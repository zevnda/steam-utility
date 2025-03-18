namespace SteamUtility.Models
{
    public class StatData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int Permission { get; set; }
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
        public object DefaultValue { get; set; }
        public object Value { get; set; }
        public bool IncrementOnly { get; set; }
    }
}
