namespace OnlineQuiz.Utilities
{
    public static class QuestionTypeConstants
    {
        public const string Single = "Single";
        public const string Multiple = "Multiple";
        public const string Text = "Text";
        
        public static readonly string[] ValidTypes = { Single, Multiple, Text };
        
        public static bool IsValid(string type)
        {
            return Array.Exists(ValidTypes, t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
        }
        
        public static bool RequiresChoices(string type)
        {
            return type == Single || type == Multiple;
        }
        
        public static bool IsEssayType(string type)
        {
            return type == Text;
        }
    }
}
