# Javascript for Grasshopper

## Features

### Javascript

Write Javascript with modern syntax. `async` and `await` by default.

### Typescript

First-class support for TypeScript. You'll get typed variable names and can provide type hints from the parameters in Grasshopper.

### Hot Reload

Every time you save your code files, your component will automatically execute with the updated code.

### Debugging

Hit breakpoints, catch exceptions and step through your code by attaching a debugger. This can be Visual Studio Code, Google Chrome, or anything else that supports it.

### Bundling

Your runtime code is automatically bundled and stored in the component. This means that you can `npm install` to your hearts content.

No dependencies (aside from this plugin) are required to execute the bundled component, so you can distribute it without conflicts or additional install steps.

Your source code is bundled with the component and saved with your Grasshopper definition, so it can be retrieved any time.

### NodeJS

The execution environment is NodeJS, meaning you have access to a wide range of inbuilt tools like `fs`. Anything that runs on node, will run here.

### Edit in your IDE

You can edit the component code in any IDE you like. We'll try to launch Visual Studio Code with a clean workspace folder, but otherwise we'll just open the script with whatever your system to defaults to. Hot reload will work either way.

If you'd like to see "official" support for another, please create an issue.
