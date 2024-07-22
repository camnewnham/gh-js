// Test input
export async function runScript(inputs: Component.Inputs): Promise<Component.Outputs> {
  return {
    x: inputs.a + inputs.b
  };
}
