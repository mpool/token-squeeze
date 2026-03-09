public struct PointStruct
{
    public double X;
    public double Y;

    public double Distance()
    {
        return Math.Sqrt(X * X + Y * Y);
    }
}

public record UserRecord(string Name, string Email)
{
    public bool Validate()
    {
        return !string.IsNullOrEmpty(Name);
    }
}

public delegate void EventCallback(object sender, EventArgs args);

public class Container
{
    public string Name { get; set; } = "";

    public void Process()
    {
        // processing logic
    }
}

public class Outer
{
    public class Inner
    {
        public void InnerMethod()
        {
            // inner logic
        }
    }
}
