namespace Demo.Embedding.Web.Plugins.FunctionTemplates;

public static class ProductFunctionTemplates
{
    public static string promptTemplateConfig = """
    template_format: semantic-kernel
    template: {prompt}
    execution_settings:
      default:
        function_choice_behavior:
          type: required
          functions:
            - ProductPlugin.GetAllProducts
            - ProductPlugin.GetProductById
            - ProductPlugin.UpdateProductPrice
            - ProductPlugin.GetProductByPrice
            - ProductPlugin.GetProductByDescription
            - ProductPlugin.GetProductByBudget
    """;
}
