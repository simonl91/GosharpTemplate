# GosharpTemplate
This is a lightweight, fast, dependency free, feature incomplete, not production ready Go-style templating library.

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
      I do not like i when the templates starts to look like code. Keep It Simple.
- when
- range
    - continue and break is NOT implemented yet
- Simple if statements 
    - Only supports a boolean variable as condition
    - {{else if}} is currently not supported, use nested ifs if needed:

## Security Warning
#### Injection
Templates are not injection safe.
It is assumed that the template author and the data is trusted.
If you are using this for HTML you can sanitize the data your self using something like:
System.Web.HttpUtility.HtmlEncode / .HtmlAttributeEncode / .UrlEncode.

## Contribute
This is a hobby/learning project, and i have limited time to work on this.
If you are missing any features, or want to contribute please let me know by making a PR or an issue.
