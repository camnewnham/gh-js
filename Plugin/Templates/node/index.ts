import esbuild, { BuildFailure } from "esbuild";

export async function bundle(
  entryPoint: string,
  outFile: string,
  minify?: boolean
) {
  try {
    await esbuild.build({
      entryPoints: [entryPoint],
      bundle: true,
      outfile: outFile,
      platform: "node",
      target: "es2022",
      sourcemap: "inline",
      minify: minify ?? false,
    });
    return true;
  } catch (e) {
    const { errors } = e as BuildFailure;
    if (errors) {
      for (let err of errors) {
        console.error(err.text);
        err.detail && console.error(err.detail);
        err.notes && console.error(err.notes);
        err.location &&
          console.error(
            `at ${getFileName(err.location.file)}:${err.location.line}:${
              err.location.column
            }`
          );
      }
    }
    return false;
  }
}

function getFileName(path: string) {
  return path.split("\\")!.pop()!.split("/")!.pop();
}
