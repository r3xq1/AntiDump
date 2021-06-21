namespace AntiDump.Helpers
{
    using dnlib.DotNet;
    using System.Collections.Generic;

    public static partial class InjectHelper
    {
        public class InjectContext : ImportMapper
        {
            public readonly Dictionary<IDnlibDef, IDnlibDef> map = new Dictionary<IDnlibDef, IDnlibDef>();
            public readonly ModuleDef OriginModule, TargetModule;
            public readonly Importer importer;
            public InjectContext(ModuleDef module, ModuleDef target)
            {
                OriginModule = module;
                TargetModule = target;
                importer = new Importer(target, ImporterOptions.TryToUseTypeDefs, new GenericParamContext(), this);
            }
            public Importer Importer => importer;
            public override ITypeDefOrRef Map(ITypeDefOrRef typeDefOrRef) => typeDefOrRef is TypeDef typeDef && map.ContainsKey(typeDef) ? (TypeDef)map[typeDef] : null;
            public override IMethod Map(MethodDef methodDef) => map.ContainsKey(methodDef) ? (MethodDef)map[methodDef] : null;
            public override IField Map(FieldDef fieldDef) => !map.ContainsKey(fieldDef) ? null : (FieldDef)map[fieldDef];
        }
    }
}