namespace FileTrackingAndProcessingServices.WebApi.Middleware
{
    /// <summary>
    /// Boru hattının en dışında durur ve altındaki tüm katmanlarda oluşan
    /// yakalanmamış hataları tek noktadan ele alır.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Bir sonraki middleware'i (ve nihayetinde controller'ı) çalıştır
                await _next(context);
            }
            catch (Exception ex)
            {
                // Detay sunucu tarafında loglanır — istemciye gönderilmez.
                _logger.LogError(ex, "Beklenmeyen hata oluştu. Yol: {Path}", context.Request.Path);

                // Cevap gövdesi yazılmaya başlandıysa artık durum kodu değiştirilemez.
                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                // İstemciye sade ve tutarlı bir cevap. traceId, bu isteği
                // sunucu loglarındaki kayıtla eşleştirmeyi sağlar.
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Sunucuda beklenmeyen bir hata oluştu.",
                    traceId = context.TraceIdentifier
                });
            }
        }
    }
}
