using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Newtonsoft.Json;
using Telegram.Bot.Types.Enums;
using System.Net;

namespace ActasFunctions
{
    public class SetUpBot
    {
        private readonly ILogger<SetUpBot> _logger;

        private readonly TelegramBotClient _botClient;

        public SetUpBot(ILogger<SetUpBot> logger)
        {
            _logger = logger;
            _botClient = new TelegramBotClient(System.Environment.GetEnvironmentVariable("TelegramBotToken", EnvironmentVariableTarget.Process));
        }

        private const string SetUpFunctionName = "setup";
        private const string UpdateFunctionName = "handleupdate";

        [Function(SetUpFunctionName)]
        public async Task RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            var handleUpdateFunctionUrl = System.Environment.GetEnvironmentVariable("HandleUpdateFunctionUri", EnvironmentVariableTarget.Process) ??
                req.Url.ToString().Replace(SetUpFunctionName, UpdateFunctionName, ignoreCase: true, culture: CultureInfo.InvariantCulture);
            Console.WriteLine($"Setting webhook at {handleUpdateFunctionUrl}");
            await _botClient.SetWebhookAsync(handleUpdateFunctionUrl);
        }

        [Function(UpdateFunctionName)]
        public async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var request = await req.ReadAsStringAsync();
            var update = JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(request);

            if (update.Type != UpdateType.Message)
                return;
            if (update.Message!.Type != MessageType.Text || update.Message.Text == null)
                return;

            await _botClient.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: GetBotResponseForInput(update.Message.Text),
                disableWebPagePreview: true
            );
        }

        private string GetBotResponseForInput(string text)
        {
            try
            {
                if (text.StartsWith('V'))
                {
                    text = text[1..];
                }

                if (int.TryParse(text, out int cid))
                {
                    var url = $"https://37latuqm766patrerdf5rvdhqe0wgrug.lambda-url.us-east-1.on.aws/?cedula=V{cid}&recaptcha=placeholder";

                    var request = WebRequest.Create(url);
                    request.Method = "GET";

                    using var webResponse = request.GetResponse();
                    using var webStream = webResponse.GetResponseStream();

                    using var reader = new StreamReader(webStream);
                    dynamic obj = JsonConvert.DeserializeObject(reader.ReadToEnd());
                    if (obj == null)
                    {
                        return GetErrorMessage("La petición para conseguir tu acta retornó información incorrecta. Esto puede ser un problema temporal. Por favor, intente de nuevo más tarde.");
                    }
                    return $"En el siguiente link puedes encontrar tu acta: {obj.url}";
                }
                else
                {
                    return "Por favor, dime un número de cédula válido, solo los dígitos.";
                }
            }
            catch (WebException)
            {
                return GetErrorMessage("Un error ha ocurrido obteniendo tu acta. Esto puede ser un problema temporal del lado del servidor. Disculpe y vuelva a intentar más tarde.");
            }
            catch (Exception)
            {
                return GetErrorMessage("Ocurrió un problema al intentar obtener tu acta. Disculpe y vuelva a intentar más tarde.");
            }
        }

        private static string GetErrorMessage(string message)
        {
            return $"{message}\nPuedes intentar encontrar tu acta en el siguiente enlace: https://resultadosconvzla.com/";
        }
    }
}
