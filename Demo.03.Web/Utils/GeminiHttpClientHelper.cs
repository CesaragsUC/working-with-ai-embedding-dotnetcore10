namespace Demo.Embedding.Web;

public static class GeminiHttpClientHelper
{
    public static HttpClient CreateGeminiHttpClient(bool ignoreSslErrors = false)
    {
        var handler = new HttpClientHandler
        {
            // Evita erro quando a rede bloqueia checagem de revogação (CRL/OCSP)
            CheckCertificateRevocationList = false
        };

        if (ignoreSslErrors)
        {
            // DEV ONLY
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return new HttpClient(handler);
    }
}

