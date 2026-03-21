using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using SalesCsvApi;

var builder = WebApplication.CreateBuilder(args);

// Configuración (App Settings en App Service)
var cfg = builder.Configuration;

#region Extraer la URL del Key Vault de Azure desde appsettings.json
//var keyVaultUrl = builder.Configuration["KeyVault:VaultUri"]; //cfg["KeyVault:Url"]; // Existen dos formas de poder acceder al appSettings.json

//if(!string.IsNullOrWhiteSpace(keyVaultUrl))
//{
//    // DefaultAzureCredential intentará autenticarse usando VS, Azure CLI o Managed Identity
//    cfg.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
//}

//// Opción A: Usando el indexador directo (devuelve un string o null)
//var apiKey = cfg["MiApi:ApiKey"];
//var apiTest = cfg["cadena-conexion"];

//// Opción B: Usando GetValue (útil si necesitas parsear a otro tipo de dato, como un int)
//var maxRetryCount = cfg.GetValue<string>("cadena-conexion", defaultValue: "0");
#endregion

#region Extraer la URL del Azure App Configuration desde appsettings.json
// Obtenemos la URL de App Configuration
//var appConfigEndpoint = builder.Configuration["AppConfig:Endpoint"];

//if(!string.IsNullOrEmpty(appConfigEndpoint))
//{
//    cfg.AddAzureAppConfiguration(options =>
//    {
//        var defaultOptions = new DefaultAzureCredentialOptions
//        {
//            // En algunos casos, como en despliegues a ciertos entornos, puede ser necesario especificar el tenant ID
//            TenantId = "d20a9516-617b-4700-8e7c-cafc939164dc"
//        };
//        var credential = new DefaultAzureCredential(defaultOptions);

//        // 1. Nos conectamos a App Configuration
//        options.Connect(new Uri(appConfigEndpoint), credential)

//               // 2. Le decimos cómo resolver las referencias a Key Vault
//               .ConfigureKeyVault(kvOptions =>
//               {
//                   kvOptions.SetCredential(credential);
//               })

//               // 3. ¡LA MAGIA DE LA RECARGA DINÁMICA!
//               .ConfigureRefresh(refreshOptions =>
//               {
//                   // Registramos una llave centinela. Si esta llave cambia en Azure,
//                   // .NET sabrá que debe recargar TODA la configuración.
//                   refreshOptions.Register("Configuracion:Centinela", refreshAll: true)
//                                 // Revisa en Azure si hay cambios máximo cada 30 segundos
//                                 .SetRefreshInterval(TimeSpan.FromSeconds(30));
//               });
//    });

//    var valorAzureAppConfig = cfg["soyKeyVault-CadenaConexion"];
//}
#endregion

builder.Services.AddControllers(); // Habilita el soporte para Controllers

// Solo cargar Azure si NO estamos en desarrollo (o si quieres probar la nube)
if(!builder.Environment.IsDevelopment())
{
    string connectionString = builder.Configuration["AppConfig:Endpoint"];

    if(string.IsNullOrEmpty(connectionString))
    {
        // Esto saldrá en tus logs de Azure y sabrás exactamente qué falta
        throw new InvalidOperationException("Falta la variable de entorno 'AppConfig__Endpoint' en Azure App Service.");
    }

    // 1. Cargar Azure App Configuration
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(connectionString), new DefaultAzureCredential())
            // 1. Cargar configuraciones normales
            .Select(KeyFilter.Any, LabelFilter.Null)
            // 2. Cargar configuraciones dinámicas con un "Sentinel" para refresco
            .ConfigureRefresh(refresh =>
            {
                refresh.Register("Sentinel", refreshAll: true)
                       .SetRefreshInterval(TimeSpan.FromMinutes(1));
            })
            // 3. Integrar Key Vault de forma transparente
            .ConfigureKeyVault(kv =>
            {
                kv.SetCredential(new DefaultAzureCredential());
            })
            // 4. Habilitar Feature Flags
            .UseFeatureFlags();
    });
}

builder.Services.AddAzureAppConfiguration(); // Necesario para el refresco dinámico

// 2. Mapeo Híbrido
// Registramos la parte compleja en una clase
builder.Services.Configure<MySettings>(
    builder.Configuration.GetSection("ConfiguracionWeb")
);

var app = builder.Build();

app.MapGet("/", () => "Hello World! ESTOY SIENDO DESPLEGADO POR PIPELINES");

app.MapControllers(); //Mapea las rutas de tus controladores

app.Run();
