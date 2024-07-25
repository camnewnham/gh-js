import esbuild from "esbuild";

export function bundle(entryPoint, outFile, minify) {
  esbuild.build({
    entryPoints: [entryPoint],
    bundle: true,
    outfile: outFile,
    platform: "node",
    target: "node14",
    sourcemap: "inline",
    minify: minify
  });
}
