using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;

namespace CleanupKuduCIFunctionalTestResources
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Authenticate with Azure
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            // Replace with your subscription ID and resource group name
            string subscriptionId = "72d082d4-9418-4b39-b5f3-242d0bad2b3f";
            string resourceGroupName = "dev-ci-kudu-functional";

            // Get the resource group
            var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName);

            // Get all web apps in the resource group
            var webApps = resourceGroup.GetWebSites().GetAll().ToList();

            // Process each web app in parallel to remove them           
            await ProcessInBatchesAsync(webApps, RemoveWebAppWithRetryAsync, 4);


            Console.WriteLine("All Web Apps have been removed.");

            // Delete all App Service Plans
            var appServicePlans = resourceGroup.GetAppServicePlans().GetAll().ToList();
            await ProcessInBatchesAsync(appServicePlans, RemoveAppServicePlanWithRetryAsync, 4);
            Console.WriteLine("All App service plans have been removed.");
        }


        // Generic method to process resources in batches
        static async Task ProcessInBatchesAsync<T>(List<T> resources, Func<T, Task> deleteFunc, int batchSize)
        {
            for (int i = 0; i < resources.Count; i += batchSize)
            {
                var batch = resources.Skip(i).Take(batchSize).ToList();
                var tasks = batch.Select(resource => deleteFunc(resource)).ToList();
                await Task.WhenAll(tasks);
            }
        }

        // Asynchronous method to remove a web app       
        // Retry logic for web app deletion with exponential backoff
        static async Task RemoveWebAppWithRetryAsync(WebSiteResource webApp)
        {
            int maxRetries = 5;
            int retryCount = 0;
            int delay = 2000; // Initial delay in milliseconds (2 seconds)

            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine($"Removing Web App: {webApp.Data.Name}");
                    await webApp.DeleteAsync(WaitUntil.Completed);
                    Console.WriteLine($"Removed Web App: {webApp.Data.Name}");
                    break; // Exit the loop on success
                }
                catch (RequestFailedException ex) when (ex.Status == 429)
                {
                    retryCount++;
                    if (retryCount == maxRetries)
                    {
                        Console.WriteLine($"Failed to delete Web App: {webApp.Data.Name} after {maxRetries} retries.");
                        throw;
                    }

                    Console.WriteLine($"Rate limit hit while deleting Web App: {webApp.Data.Name}. Retrying in {delay / 1000} seconds...");
                    await Task.Delay(delay);
                    delay *= 2; // Exponential backoff
                }
            }
        }

        // Retry logic for App Service Plan deletion with exponential backoff
        static async Task RemoveAppServicePlanWithRetryAsync(AppServicePlanResource appServicePlan)
        {
            int maxRetries = 5;
            int retryCount = 0;
            int delay = 2000; // Initial delay in milliseconds (2 seconds)

            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine($"Removing App Service Plan: {appServicePlan.Data.Name}");
                    await appServicePlan.DeleteAsync(WaitUntil.Completed);
                    Console.WriteLine($"Removed App Service Plan: {appServicePlan.Data.Name}");
                    break; // Exit the loop on success
                }
                catch (RequestFailedException ex) when (ex.Status == 429)
                {
                    retryCount++;
                    if (retryCount == maxRetries)
                    {
                        Console.WriteLine($"Failed to delete App Service Plan: {appServicePlan.Data.Name} after {maxRetries} retries.");
                        throw;
                    }

                    Console.WriteLine($"Rate limit hit while deleting App Service Plan: {appServicePlan.Data.Name}. Retrying in {delay / 1000} seconds...");
                    await Task.Delay(delay);
                    delay *= 2; // Exponential backoff
                }
            }
        }
    }
}
