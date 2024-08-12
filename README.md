# Javascript for Grasshopper

## Features

### JavaScript

Write Javascript with modern syntax. `async` and `await` by default.

![javascript component example](https://github.com/user-attachments/assets/b663f617-29af-431c-9fc1-129973eaf983)

```js
export async function runScript(inputs) {
    const sum = (inputs.a ?? 0) + (inputs.b ?? 0);
    return {
        x: sum
    };
}
```

### TypeScript

First-class support for TypeScript. Variables are fully typed, and type hints can be provided from from the parameters in Grasshopper.

![image](https://github.com/user-attachments/assets/65b7e44c-258d-47e5-a8df-48456f2737ef)

### RhinoCommon Support
The Rhino and Grasshopper namespaces are accessible via the global 'dotnet' object. The current Rhino and Grasshopper documents are provided through the context object.

![image](https://github.com/user-attachments/assets/b7a50cf0-e68c-4e16-9070-ac62a1fa30fa)

Access to other dotnet types may be made available in future.

### Hot Reload

Every time you save your code files, your component will automatically execute with the updated code.

![hot_reload](https://github.com/user-attachments/assets/f3afcc83-acf3-4083-a1e7-085fb26e42c2)

### Console Output

`console.log()` `info()` `warn()` and `error()` are returned via the `out` parameter, in addition to any syntax errors or exceptions.

![image](https://github.com/user-attachments/assets/be06ac32-b958-4dbd-9469-a5520a0f0aef)

### Debugging

Hit breakpoints, catch exceptions and step through your code by attaching a debugger. This can be Visual Studio Code, Google Chrome, or anything else that supports it.

![image](https://github.com/user-attachments/assets/42c2e53f-55a3-4351-abb7-c0a64ab339b5)

### Bundling

Editor code is automatically bundled for runtime execution, minified and stored in the Grasshopper component. This means most js/node modules are supported via `npm install`.

![image](https://github.com/user-attachments/assets/88250f92-2283-4744-ae7d-fa38d31325c0)

No dependencies (aside from this plugin) are required to execute the bundled component, so you can distribute it without conflicts or additional install steps.

Your source code is bundled with the component and saved with your Grasshopper definition, so it can be retrieved any time.

Syntax errors are output through through the comoponent (though any reasonable linter should pick them up first.)

![image](https://github.com/user-attachments/assets/0a33c70d-9d1c-4411-93c7-d5320937b115)

### NodeJS

The execution environment is NodeJS, meaning you have access to a wide range of inbuilt tools like `fs`. Anything that runs on node, will run here.

### Edit in your IDE

Component code can be edited in any IDE. This plugin will attempt to launch Visual Studio Code primarily, but otherwise the the index file will just open the script with the system default. 

No need to run scripts (or even have node installed) - automatic bundling and hot reload will work either way.

![image](https://github.com/user-attachments/assets/4d70079e-d1ff-4e21-b04a-c5076d3550dc)

If you'd like to see "official" support for another IDE, please create an issue.
