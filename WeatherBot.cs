using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBotWeather
{
    public class WeatherBot
    {
        TelegramBotClient botClient = new TelegramBotClient("7341834973:AAGYSQ9SvU8M7TH9RIkj7MMr-oEM7qj4mBA");
        CancellationToken cancellationToken = new CancellationToken();
        ReceiverOptions receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
        private readonly HttpClient httpClient = new HttpClient();
        private readonly ConcurrentDictionary<long, string> userStates = new ConcurrentDictionary<long, string>();

        public async Task Start()
        {
            botClient.StartReceiving(HandlerUpdateAsync, HandlerError, receiverOptions, cancellationToken);
            var botMe = await botClient.GetMeAsync();
            Console.WriteLine($"Bot {botMe.Username} started");
            Console.ReadKey();
        }

        private async Task HandlerError(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        //private async Task HandlerError(ITelegramBotClient client, Exception exception, CancellationToken token)
        //{
        //    var errorMessage = exception switch
        //    {
        //        ApiRequestException apiRequestException => $"Telegram API error:\n{apiRequestException.ErrorCode}\n{apiRequestException.Message}",
        //        _ => exception.ToString()
        //    };
        //    Console.WriteLine(errorMessage);
        //    await Task.CompletedTask;
        //}

        private async Task HandlerUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update?.Message?.Text != null)
            {
                await HandlerMessageAsync(botClient, update.Message);
            }
        }

        private async Task HandlerMessageAsync(ITelegramBotClient botClient, Message message)
        {
            if (message.Text != null && message.Text.StartsWith("/start", StringComparison.InvariantCultureIgnoreCase))
            {
                await HandleStartCommandAsync(message);
            }
            else if (userStates.TryGetValue(message.Chat.Id, out string state))
            {
                if (state == "waiting_for_location")
                {
                    await HandleLocationInputAsync(message);
                }
                else if (state.StartsWith("waiting_for_"))
                {
                    await HandlePlansInputAsync(message, state.Substring("waiting_for_".Length));
                }
            }
            else
            {
                switch (message.Text)
                {
                    case "Отримати погоду":
                        await HandleGetWeatherCommandAsync(message);
                        break;
                    case "Показати команди":
                        await ShowCommandsAsync(message);
                        break;
                    case "Плани на відкритому повітрі":
                        await HandleGetPlansCommandAsync(message, "outdoorplans");
                        break;
                    case "Спортивні плани":
                        await HandleGetPlansCommandAsync(message, "sportplans");
                        break;
                    case "Сільськогосподарські плани":
                        await HandleGetPlansCommandAsync(message, "agricultureplans");
                        break;
                    case "Будівельні плани":
                        await HandleGetPlansCommandAsync(message, "constructionplans");
                        break;
                    default:
                        await HandleUnknownCommandAsync(message);
                        break;
                }
            }
        }

        private async Task HandleStartCommandAsync(Message message)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Отримати погоду" },
                new KeyboardButton[] { "Показати команди" },
                new KeyboardButton[] { "Плани на відкритому повітрі" },
                new KeyboardButton[] { "Спортивні плани" },
                new KeyboardButton[] { "Сільськогосподарські плани" },
                new KeyboardButton[] { "Будівельні плани" }
            })
            {
                ResizeKeyboard = true
            };

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Привіт! Я WeatherBot. Я можу надати вам інформацію про поточну погоду та поради щодо діяльності. Виберіть команду:",
                replyMarkup: replyKeyboardMarkup
            );
        }

        private async Task HandleGetWeatherCommandAsync(Message message)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Будь ласка, введіть ваше місцезнаходження у форматі 'широта,довгота'.",
                replyMarkup: new ReplyKeyboardRemove()
            );

            userStates[message.Chat.Id] = "waiting_for_location";
        }

        private async Task HandleLocationInputAsync(Message message)
        {
            var location = message.Text.Split(',');
            if (location.Length == 2 &&
                double.TryParse(location[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(location[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
            {
                var weather = await GetWeatherAsync(lat, lon);
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: weather,
                    replyMarkup: GetCommandKeyboard() // Show commands keyboard after providing the weather
                );

                userStates.TryRemove(message.Chat.Id, out _);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Невірний формат місцезнаходження. Будь ласка, введіть ваше місцезнаходження у форматі 'широта,довгота'."
                );

                userStates[message.Chat.Id] = "waiting_for_location";
            }
        }

        private async Task HandlePlansInputAsync(Message message, string planType)
        {
            var location = message.Text.Split(',');
            if (location.Length == 2 &&
                double.TryParse(location[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(location[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
            {
                var plans = await GetPlansAsync(lat, lon, planType);
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: plans,
                    replyMarkup: GetCommandKeyboard() // Show commands keyboard after providing the plans
                );

                userStates.TryRemove(message.Chat.Id, out _);
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Невірний формат місцезнаходження. Будь ласка, введіть ваше місцезнаходження у форматі 'широта,довгота'."
                );

                userStates[message.Chat.Id] = $"waiting_for_{planType}";
            }
        }

        private async Task ShowCommandsAsync(Message message)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Ось доступні команди. Виберіть одну:",
                replyMarkup: GetCommandKeyboard()
            );
        }

        private async Task HandleUnknownCommandAsync(Message message)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Вибачте, я не розумію цієї команди. Будь ласка, виберіть одну з доступних функцій.",
                replyMarkup: GetCommandKeyboard()
            );
        }

        private async Task HandleGetPlansCommandAsync(Message message, string planType)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Будь ласка, введіть ваше місцезнаходження у форматі 'широта,довгота'.",
                replyMarkup: new ReplyKeyboardRemove()
            );

            userStates[message.Chat.Id] = $"waiting_for_{planType}";
        }

        private async Task<string> GetWeatherAsync(double lat, double lon)
        {
            try
            {
                var windSpeedResponse = await httpClient.GetStringAsync($"https://localhost:7201/weather/windspeed?lat={lat}&lon={lon}");
                var temperatureResponse = await httpClient.GetStringAsync($"https://localhost:7201/weather/temperature?lat={lat}&lon={lon}");
                var descriptionResponse = await httpClient.GetStringAsync($"https://localhost:7201/weather/description?lat={lat}&lon={lon}");
                var clothingResponse = await httpClient.GetStringAsync($"https://localhost:7201/weather/clothing?lat={lat}&lon={lon}");

                var windSpeed = float.Parse(windSpeedResponse, CultureInfo.InvariantCulture);
                var temperature = float.Parse(temperatureResponse, CultureInfo.InvariantCulture);
                var description = descriptionResponse;
                var clothingAdvice = clothingResponse;

                return $"Інформація про погоду:\n" +
                       $"- Опис: {description}\n" +
                       $"- Температура: {temperature} °C\n" +
                       $"- Швидкість вітру: {windSpeed} м/с\n" +
                       $"- Поради щодо одягу: {clothingAdvice}";
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Помилка запиту: {e.Message}");
                return "Вибачте, я не можу отримати інформацію про погоду. Спробуйте ще раз.";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Несподівана помилка: {e.Message}");
                return "Вибачте, щось пішло не так. Спробуйте ще раз.";
            }
        }

        private async Task<string> GetPlansAsync(double lat, double lon, string planType)
        {
            try
            {
                var planResponse = await httpClient.GetStringAsync($"https://localhost:7201/weather/{planType}?lat={lat}&lon={lon}");
                return planResponse;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Помилка запиту: {e.Message}");
                return "Вибачте, я не можу отримати інформацію про плани. Спробуйте ще раз.";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Несподівана помилка: {e.Message}");
                return "Вибачте, щось пішло не так. Спробуйте ще раз.";
            }
        }

        private IReplyMarkup GetCommandKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Отримати погоду" },
                new KeyboardButton[] { "Плани на відкритому повітрі" },
                new KeyboardButton[] { "Спортивні плани" },
                new KeyboardButton[] { "Сільськогосподарські плани" },
                new KeyboardButton[] { "Будівельні плани" }
            })
            {
                ResizeKeyboard = true
            };
        }
    }
}
