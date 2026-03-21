using RevitStartup;
using System.Reflection;

try
{
    AppStartup appStartup = new AppStartupTest(new UiHandler());
    string assemblyPath = Assembly.GetExecutingAssembly().Location;
    appStartup.Run(assemblyPath);

    Console.ReadLine();
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
}