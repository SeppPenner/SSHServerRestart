// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The main program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SSHServerRestart;

/// <summary>
/// The main program.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main method.
    /// </summary>
    public static void Main()
    {
        try
        {
            var config = InitConfiguration(AppDomain.CurrentDomain.BaseDirectory + "Config.xml") ?? new();
            Console.WriteLine("Konfiguration geladen.");

            using var client = new SshClient(config.ServerName, config.ServerPort, config.User, config.Password);
            Restart(client);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    /// <summary>
    /// Does a restart via SSH.
    /// </summary>
    /// <param name="client">The SSH client.</param>
    private static void Restart(SshClient client)
    {
        client.Connect();
        Console.WriteLine("Prozesse werden beendet");
        client.RunCommand("execute /usr/syno/bin/syno_poweroff_task");
        Thread.Sleep(7000);
        Console.WriteLine("Server wird neu gestartet");
        client.RunCommand("reboot");
        client.Disconnect();
        Console.WriteLine("Beliebige Taste drücken, um das Programm zu beenden\n");
        Console.ReadKey();
    }

    /// <summary>
    /// Initializes the configuration.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The <see cref="Config"/>.</returns>
    private static Config? InitConfiguration(string fileName)
    {
        try
        {
            var xDocument = XDocument.Load(fileName);
            return CreateObjectsFromString<Config?>(xDocument);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return null;
        }
    }

    /// <summary>
    /// Creates a object from the <see cref="string"/>.
    /// </summary>
    /// <typeparam name="T">The type parameter.</typeparam>
    /// <param name="xDocument">The X document.</param>
    /// <returns>A object of type <see cref="T"/>.</returns>
    private static T? CreateObjectsFromString<T>(XDocument xDocument)
    {
        var xmlSerializer = new XmlSerializer(typeof(T));
        return (T?)xmlSerializer.Deserialize(new StringReader(xDocument.ToString()));
    }
}
