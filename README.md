# GosharpTemplate
This is a lightweight, dependency free, feature incomplete, **NOT** production ready Go-style HTML templating library.

![Example 1](https://raw.githubusercontent.com/simonl91/GosharpTemplate/refs/heads/main/img/usage_example.png)

![Result 1](https://raw.githubusercontent.com/simonl91/GosharpTemplate/refs/heads/main/img/usage_example_result.png)

## Security
#### Injection
Templates are not injection safe at the moment.
It is assumed that the template author and the data is trusted.
You can sanitize the data your self using something like:
System.Web.HttpUtility.HtmlEncode / .HtmlAttributeEncode / .UrlEncode.

#### Runtime Errors
The current error model is to crash if something is wrong.

## Supported features so far:
- define
- block
- template
- data parameters
- range
    - continue and break are not implemented yet
- Simple if statements 
    - Only supports a boolean variable as condition
    - {{else if}} is currently not supported

## Contribute
This is a hobby/learning project, and i have limited time to work on this.
If you are missing any features, or want to contribute please let me know by making a PR or an issue.
