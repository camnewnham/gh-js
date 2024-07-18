// Test input
export async function runScript(inputs: { [key: string]: any }) {
  console.info("Started");
  await new Promise((resolve) => setTimeout(resolve, 250));
  console.warn("Completed");

  return {
    square: inputs.test * inputs.test * 2 * 2,
  };
}
