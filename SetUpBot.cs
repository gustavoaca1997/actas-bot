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
            var handleUpdateFunctionUrl = req.Url.ToString().Replace(SetUpFunctionName, UpdateFunctionName,
                                                ignoreCase: true, culture: CultureInfo.InvariantCulture);
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

                var url = $"https://tvtcrhau2vo336qa5r66p3bygy0hazyk.lambda-url.us-east-1.on.aws/?cedula=V{text}";

                var request = WebRequest.Create(url);
                request.Method = "GET";

                using var webResponse = (HttpWebResponse)request.GetResponse();
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    return "Acta no disponible o cédula incorrecta. Revisa el número de cédula o intenta más tarde.";
                }

                using var webStream = webResponse.GetResponseStream();

                using var reader = new StreamReader(webStream);
                dynamic obj = JsonConvert.DeserializeObject(reader.ReadToEnd());
                return $"En el siguiente link puedes encontrar tu acta: {obj.url}";
            }
            catch
            {
                return "Por favor, dime un número de cédula válido.";
            }
        }
    }
}
