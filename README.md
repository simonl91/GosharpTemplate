# GosharpTemplate
This is a lightweight, fast, easy to use, dependency free, Go-style templating library.
It is NOT a goal to implement all the features in available in Go Templates.
I do NOT think views/templates should be full of logic. Keep It Simple.

![Example 1](https://raw.githubusercontent.com/simonl91/GosharpTemplate/refs/heads/main/img/usage_example.png)

![Result 1](https://raw.githubusercontent.com/simonl91/GosharpTemplate/refs/heads/main/img/usage_example_result.png)

## Supported features so far:
- '-' to trim whitespace
- define
- block
- template
- pipelines
    - Only data member access ex: 
      ```.Title```. or ```.Address.Street```
    - No function or method calls
- with
- range
    - continue and break is NOT implemented yet
- Simple if statements 
    - Only supports a boolean variable as condition so far
    - {{else if}} is currently not supported, use nested ifs if needed

## Injection
Templates are not html injection safe.
It is assumed that the template author and the data is trusted.
If you are using this for where users can input data,
you can sanitize the data using something like:
System.Web.HttpUtility.HtmlEncode / .HtmlAttributeEncode / .UrlEncode.

## Contribute
This is a hobby/learning project, and i have limited time to work on this.
If you are missing any features, or want to contribute please let me know by making a PR or an issue.
