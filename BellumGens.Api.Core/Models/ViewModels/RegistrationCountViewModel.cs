namespace BellumGens.Api.Core.Models
{
    public class RegistrationCountViewModel
    {
        public RegistrationCountViewModel(int number, Game gameType)
        {
            game = gameType;
            count = number;
        }
        public Game game { get; set; }
        public int count { get; set; }
    }
}