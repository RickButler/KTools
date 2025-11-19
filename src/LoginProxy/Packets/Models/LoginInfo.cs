namespace LoginProxy.Packets.Models;

public class LoginInfo
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Username}:(hidden)";
    }
}