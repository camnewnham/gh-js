namespace JavascriptForGrasshopper.CodeGenerator
{
    public struct TypeDefinition
    {
        public string Type;
        public string Name;
        public string VariableName;
        public string Description;
        public bool Optional;

        public string Definition => $"{VariableName}: {Type};";
    }

    public partial class ComponentTypeGenerator
    {
        public readonly TypeDefinition[] Inputs;
        public readonly TypeDefinition[] Outputs;

        public ComponentTypeGenerator(TypeDefinition[] inputs, TypeDefinition[] outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }
    }
}
