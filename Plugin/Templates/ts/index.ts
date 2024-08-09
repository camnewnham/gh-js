/// <reference types="./types/component.d.ts" />
/**
 * Executes within the context of the Grasshopper component.
 * @param {Inputs} inputs An object with keys matching the inputs to the Grasshopper component
 * @param {Context} context The context in which the Grasshopper component is executed
 * @returns {Outputs} An object with keys matching the outputs from the Grasshopper component
 */

export async function runScript(inputs: Inputs, context: Context): Promise<Outputs> {
  const sum: number = (inputs.a ?? 0) + (inputs.b ?? 0);
  return {
      x: sum
  };
}
