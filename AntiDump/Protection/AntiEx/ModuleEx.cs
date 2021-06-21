namespace AntiDump.Protection.AntiEx
{
    using System.Collections.Generic;
    using System.Linq;
    using dnlib.DotNet;
    using dnlib.DotNet.Emit;
    using Helpers;

    public static class ModuleEx
    {
        public static void Execute(ModuleDef mod)
        {
            ModuleDefMD typeModule = ModuleDefMD.Load(typeof(DumpEx).Module);
            MethodDef cctor = mod.GlobalType.FindOrCreateStaticConstructor();
            TypeDef typeDef = typeModule.ResolveTypeDef(MDToken.ToRID(typeof(DumpEx).MetadataToken));
            IEnumerable<IDnlibDef> members = InjectHelper.Inject(typeDef, mod.GlobalType, mod);
            MethodDef init = (MethodDef)members.Single(method => method.Name == "Protection");
            cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));
            foreach (MethodDef md in mod.GlobalType.Methods)
            {
                if (md.Name != ".ctor") continue;
                mod.GlobalType.Remove(md);
                break;
            }
        }
    }
}