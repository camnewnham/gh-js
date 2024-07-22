// Test input
export async function runScript(inputs: Component.Inputs): Promise<Component.Outputs> {
  return {
    sum: inputs.a + inputs.b
  };
}
