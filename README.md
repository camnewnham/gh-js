# Javascript for Grasshopper

## Features

### Javascript

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

### Typescript

First-class support for TypeScript. You'll get typed variable names and can provide type hints from the parameters in Grasshopper.

![typescript component example](https://github.com/user-attachments/assets/683e0c5a-6ca8-459a-accb-4dcc593fbafa)

```ts
export async function runScript(inputs: Component.Inputs): Promise<Component.Outputs> {
    const sum: number = (inputs.a ?? 0) + (inputs.b ?? 0);
  return {
      x: sum
  };
}
```

Use the right click menu to add additional type information which will be used by your linter.

![image](https://github.com/user-attachments/assets/feea9e03-40b7-4a4f-a20c-65d2e4f99105)
![image](https://github.com/user-attachments/assets/f9e1b257-7da2-4058-8a68-07d6a4194a99)

### Hot Reload

Every time you save your code files, your component will automatically execute with the updated code.

### Console Output

`console.log()` `info()` `warn()` and `error()` are returned via the `out` parameter, in addition to any syntax errors or exceptions.

![image](https://github.com/user-attachments/assets/be06ac32-b958-4dbd-9469-a5520a0f0aef)


### Debugging

Hit breakpoints, catch exceptions and step through your code by attaching a debugger. This can be Visual Studio Code, Google Chrome, or anything else that supports it.

### Bundling

Your runtime code is automatically bundled and stored in the component. This means that you can `npm install` to your hearts content.

No dependencies (aside from this plugin) are required to execute the bundled component, so you can distribute it without conflicts or additional install steps.

Your source code is bundled with the component and saved with your Grasshopper definition, so it can be retrieved any time.

Syntax errors are output through through the comoponent (though any reasonable linter should pick them up first.)

![image](https://github.com/user-attachments/assets/0a33c70d-9d1c-4411-93c7-d5320937b115)

### NodeJS

The execution environment is NodeJS, meaning you have access to a wide range of inbuilt tools like `fs`. Anything that runs on node, will run here.

### Edit in your IDE

You can edit the component code in any IDE you like. We'll try to launch Visual Studio Code with a clean workspace folder, but otherwise we'll just open the script with whatever your system to defaults to. Hot reload will work either way.

If you'd like to see "official" support for another, please create an issue.
