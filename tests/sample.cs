/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    /// <summary>Gets or sets the user name.</summary>
    public string Name { get; set; }

    public User(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Greet the user.
    /// </summary>
    public string Greet()
    {
        return $"Hello, {Name}!";
    }
}

public interface IRepository<T>
{
    T FindById(int id);
}

public enum UserRole
{
    Admin,
    User
}

public record UserDto(string Name, string Email);

public struct Point
{
    public int X;
    public int Y;
}
