### Working with API using C# client

Certificate authority project has several API controllers.

Client library to work with API is an automatically generated library. When endpoints are updated client library should be regenerated.

Client can be accessed by referencing `CertificateAuthority.Client` project or by adding a nuget package: https://www.nuget.org/packages/CertificateAuthority.Client/



You can use test deployment of CA which is accessible at `http://52.233.33.138:5000/swagger/index.html`



### Generating web API client code

To automatically generate web API client implementation for existing controller(s) do the following: 

1. Run CertificateAuthority project and navigate to `https://localhost:44333/swagger/v1/swagger.json` (base url can be different). Copy contents of `swagger.json`.

2. Install and open `NSWagStudio` (https://github.com/RicoSuter/NSwag)

3. In `NSWagStudio`  change runtime to `.net core 2.2`

4. Go to `Swagger Specification` tab and paste contents of `swagger.json`

5. In the right `Outputs` window open `CSarp Client` tab
   1. Set `Namespace` to `CertificateAuthority.Client`
   2. Set `Output file path` to be smth like `C:\Users\user\Documents\GitHub\CertificateAuthority\Src\CertificateAuthority.Client\Client.generated.cs`

8. Click `Generate Files` button



### Client usage example

```
class Program
{
    public static string BaseUrl = "https://localhost:44333/";

    static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync()
    {
        Client client = new Client(BaseUrl, new HttpClient());

        ICollection<string> revokedCerts = await client.GetRevokedCertificatesAsync();
        Console.WriteLine("Revoked certificates: " + string.Join(",", revokedCerts));


        IDictionary<string, string> accessLevels = await client.GetAllAccessLevelsAsync(new CredentialsModel() { AccountId = 1, Password = "4815162342" });
        Console.WriteLine("Access levels: " + string.Join(",", accessLevels.Values));

        Console.ReadKey();
    }
}
```

