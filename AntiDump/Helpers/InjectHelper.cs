namespace AntiDump.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using dnlib.DotNet;
    using dnlib.DotNet.Emit;

    public static partial class InjectHelper
    {
        private static TypeDefUser Clone(TypeDef origin)
        {
            TypeDefUser ret = new TypeDefUser(origin.Namespace, origin.Name)
            {
                Attributes = origin.Attributes
            };

            if (origin.ClassLayout != null)
            {
                ret.ClassLayout = new ClassLayoutUser(origin.ClassLayout.PackingSize, origin.ClassSize);
            }

            foreach (GenericParam genericParam in origin.GenericParameters)
            {
                ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));
            }

            return ret;
        }
        private static MethodDefUser Clone(MethodDef origin)
        {
            MethodDefUser ret = new MethodDefUser(origin.Name, null, origin.ImplAttributes, origin.Attributes);

            foreach (GenericParam genericParam in origin.GenericParameters)
            {
                ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));
            }

            return ret;
        }
        private static FieldDefUser Clone(FieldDef origin)
        {
            FieldDefUser ret = new FieldDefUser(origin.Name, null, origin.Attributes);
            return ret;
        }
        private static TypeDef PopulateContext(TypeDef typeDef, InjectContext ctx)
        {
            TypeDef ret;
            if (ctx.map.TryGetValue(typeDef, out IDnlibDef existing))
            {
                ret = (TypeDef)existing;
            }
            else
            {
                ret = Clone(typeDef);
                ctx.map[typeDef] = ret;
            }

            foreach (TypeDef nestedType in typeDef.NestedTypes)
            {
                ret.NestedTypes.Add(PopulateContext(nestedType, ctx));
            }

            foreach (MethodDef method in typeDef.Methods)
            {
                ret.Methods.Add((MethodDef)(ctx.map[method] = Clone(method)));
            }

            foreach (FieldDef field in typeDef.Fields)
            {
                ret.Fields.Add((FieldDef)(ctx.map[field] = Clone(field)));
            }

            return ret;
        }
        private static void CopyTypeDef(TypeDef typeDef, InjectContext ctx)
        {
            TypeDef newTypeDef = (TypeDef)ctx.map[typeDef];

            newTypeDef.BaseType = ctx.Importer.Import(typeDef.BaseType);

            foreach (InterfaceImpl iface in typeDef.Interfaces)
            {
                newTypeDef.Interfaces.Add(new InterfaceImplUser(ctx.Importer.Import(iface.Interface)));
            }
        }
        private static void CopyMethodDef(MethodDef methodDef, InjectContext ctx)
        {
            MethodDef newMethodDef = (MethodDef)ctx.map[methodDef];

            newMethodDef.Signature = ctx.Importer.Import(methodDef.Signature);
            newMethodDef.Parameters.UpdateParameterTypes();

            if (methodDef.ImplMap != null)
            {
                newMethodDef.ImplMap = new ImplMapUser(new ModuleRefUser(ctx.TargetModule, methodDef.ImplMap.Module.Name),
                methodDef.ImplMap.Name, methodDef.ImplMap.Attributes);
            }

            foreach (CustomAttribute ca in methodDef.CustomAttributes)
            {
                newMethodDef.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)ctx.Importer.Import(ca.Constructor)));
            }

            if (methodDef.HasBody)
            {
                newMethodDef.Body = new CilBody(methodDef.Body.InitLocals, 
                    new List<Instruction>(),
                    new List<ExceptionHandler>(), new List<Local>()) { MaxStack = methodDef.Body.MaxStack };

                Dictionary<object, object> bodyMap = new Dictionary<object, object>();

                foreach (Local local in methodDef.Body.Variables)
                {
                    Local newLocal = new Local(ctx.Importer.Import(local.Type));
                    newMethodDef.Body.Variables.Add(newLocal);
                    newLocal.Name = local.Name;
                    newLocal.Attributes = local.Attributes;
                    bodyMap[local] = newLocal;
                }

                foreach (Instruction instr in methodDef.Body.Instructions)
                {
                    Instruction newInstr = new Instruction(instr.OpCode, instr.Operand)
                    {
                        SequencePoint = instr.SequencePoint
                    };

                    switch (newInstr.Operand)
                    {
                        case IType type: newInstr.Operand = ctx.Importer.Import(type); break;
                        case IMethod method: newInstr.Operand = ctx.Importer.Import(method); break;
                        case IField field: newInstr.Operand = ctx.Importer.Import(field); break;
                    }

                    newMethodDef.Body.Instructions.Add(newInstr);
                    bodyMap[instr] = newInstr;
                }

                foreach (Instruction instr in newMethodDef.Body.Instructions)
                {
                    if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
                    {
                        instr.Operand = bodyMap[instr.Operand];
                    }
                    else
                    {
                        if (instr.Operand is Instruction[] v)
                        {
                            instr.Operand = v.Select(target => (Instruction)bodyMap[target]).ToArray();
                        }
                    }
                }

                foreach (ExceptionHandler eh in methodDef.Body.ExceptionHandlers)
                {
                    newMethodDef.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType)
                    {
                        CatchType = eh.CatchType == null ? null : ctx.Importer.Import(eh.CatchType),
                        TryStart = (Instruction)bodyMap[eh.TryStart],
                        TryEnd = (Instruction)bodyMap[eh.TryEnd],
                        HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
                        HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
                        FilterStart = eh.FilterStart == null ? null : (Instruction)bodyMap[eh.FilterStart]
                    });
                }

                newMethodDef.Body.SimplifyMacros(newMethodDef.Parameters);
            }
        }
        private static void CopyFieldDef(FieldDef fieldDef, InjectContext ctx)
        {
            FieldDef newFieldDef = (FieldDef)ctx.map[fieldDef];

            newFieldDef.Signature = ctx.Importer.Import(fieldDef.Signature);
        }
        private static void Copy(TypeDef typeDef, InjectContext ctx, bool copySelf)
        {
            if (copySelf)
            {
                CopyTypeDef(typeDef, ctx);
            }

            foreach (TypeDef nestedType in typeDef.NestedTypes)
            {
                Copy(nestedType, ctx, true);
            }

            foreach (MethodDef method in typeDef.Methods)
            {
                CopyMethodDef(method, ctx);
            }

            foreach (FieldDef field in typeDef.Fields)
            {
                CopyFieldDef(field, ctx);
            }
        }
        public static TypeDef Inject(TypeDef typeDef, ModuleDef target)
        {
            var ctx = new InjectContext(typeDef.Module, target);
            PopulateContext(typeDef, ctx);
            Copy(typeDef, ctx, true);
            return (TypeDef)ctx.map[typeDef];
        }
        public static MethodDef Inject(MethodDef methodDef, ModuleDef target)
        {
            var ctx = new InjectContext(methodDef.Module, target);
            ctx.map[methodDef] = Clone(methodDef);
            CopyMethodDef(methodDef, ctx);
            return (MethodDef)ctx.map[methodDef];
        }
        public static IEnumerable<IDnlibDef> Inject(TypeDef typeDef, TypeDef newType, ModuleDef target)
        {
            var ctx = new InjectContext(typeDef.Module, target);
            ctx.map[typeDef] = newType;
            PopulateContext(typeDef, ctx);
            Copy(typeDef, ctx, false);
            return ctx.map.Values.Except(new[] { newType });
        }
    }
}