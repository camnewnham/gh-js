// Use "npm run dev" to develop the component

/**
 * Executes within the context of the Grasshopper component.
 * @param inputs An object with keys matching the inputs to the Grasshopper component
 * @returns An object with keys matching the outputs from the Grasshopper component
 */

export async function runScript(inputs: Component.Inputs): Promise<Component.Outputs> {
  return {
    x: inputs.a + inputs.b
  };
}
