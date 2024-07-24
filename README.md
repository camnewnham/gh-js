# Javascript for Grasshopper

## Features

### Javascript

Write Javascript with modern syntax. `async` and `await` by default.

### Typescript

First-class support for TypeScript. You'll get typed variable names and can provide type hints from the parameters in Grasshopper.

### Hot Reload

When you use `npm run dev`, your Grasshopper component will hot reload every time you save your code.

### Debugging

Hit breakpoints, catch exceptions and step through your code by attaching a debugger.

### Bundling

When you use `npm run dev` or `npm run build`, your runtime code is automatically bundled and stored in the component.

No dependencies (aside from this plugin) are required to execute the bundled component, so you can distribute it without conflicts or additional install steps.

Your source code is bundled with the component and saved with your Grasshopper definition, so it can be retrieved any time.

### Modules

`npm install` to your hearts content. Thanks to the bundler, your code will execute dependency-free.

### NodeJS

The execution environment is NodeJS, meaning you have access to a wide range of inbuilt tools like `fs`. Anything that runs on node, will run here.

### Edit in your IDE

You can edit the component code in any IDE you like -- defaulting to Visual Studio Code. If you'd like to see "official" support for another, please create an issue.
