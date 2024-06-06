using System.Threading.Tasks;
using TelegramBotWeather;

class Program
{
    static async Task Main(string[] args)
    {
        var weatherBot = new WeatherBot();
        await weatherBot.Start();
    }
}
