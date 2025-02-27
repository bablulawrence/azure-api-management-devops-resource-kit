using System;
using McMaster.Extensions.CommandLineUtils;
using System.Collections.Generic;
using Microsoft.Azure.Management.ApiManagement.ArmTemplates.Common;
using System.Linq;

namespace Microsoft.Azure.Management.ApiManagement.ArmTemplates.Extract
{
    public class ExtractCommand : CommandLineApplication
    {
        public ExtractCommand()
        {
            this.Name = GlobalConstants.ExtractName;
            this.Description = GlobalConstants.ExtractDescription;

            var sourceApimName = this.Option("--sourceApimName <sourceApimName>", "Source API Management name", CommandOptionType.SingleValue);
            var destinationAPIManagementName = this.Option("--destinationApimName <destinationApimName>", "Destination API Management name", CommandOptionType.SingleValue);
            var resourceGroupName = this.Option("--resourceGroup <resourceGroup>", "Resource Group name", CommandOptionType.SingleValue);
            var fileFolderName = this.Option("--fileFolder <filefolder>", "ARM Template files folder", CommandOptionType.SingleValue);
            var apiName = this.Option("--apiName <apiName>", "API name", CommandOptionType.SingleValue);
            var linkedTemplatesBaseUrlName = this.Option("--linkedTemplatesBaseUrl <linkedTemplatesBaseUrl>", "Creates a master template with links", CommandOptionType.SingleValue);
            var linkedTemplatesUrlQueryString = this.Option("--linkedTemplatesUrlQueryString <linkedTemplatesUrlQueryString>", "Query string appended to linked templates uris that enables retrieval from private storage", CommandOptionType.SingleValue);
            var policyXMLBaseUrlName = this.Option("--policyXMLBaseUrl <policyXMLBaseUrl>", "Writes policies to local XML files that require deployment to remote folder", CommandOptionType.SingleValue);

            this.HelpOption();

            this.OnExecute(async () =>
            {
                try
                {
                    if (!sourceApimName.HasValue()) throw new Exception("Missing parameter <sourceApimName>.");
                    if (!destinationAPIManagementName.HasValue()) throw new Exception("Missing parameter <destinationApimName>.");
                    if (!resourceGroupName.HasValue()) throw new Exception("Missing parameter <resourceGroup>.");
                    if (!fileFolderName.HasValue()) throw new Exception("Missing parameter <filefolder>.");

                    // isolate cli parameters
                    string resourceGroup = resourceGroupName.Value().ToString();
                    string sourceApim = sourceApimName.Value().ToString();
                    string destinationApim = destinationAPIManagementName.Value().ToString();
                    string fileFolder = fileFolderName.Value().ToString();
                    string linkedBaseUrl = linkedTemplatesBaseUrlName.HasValue() ? linkedTemplatesBaseUrlName.Value().ToString() : null;
                    string linkedUrlQueryString = linkedTemplatesUrlQueryString.HasValue() ? linkedTemplatesUrlQueryString.Value().ToString() : null;
                    string policyXMLBaseUrl = policyXMLBaseUrlName.HasValue() ? policyXMLBaseUrlName.Value().ToString() : null;
                    string singleApiName = null;

                    if (apiName.Values.Count > 0)
                    {
                        singleApiName = apiName.Value().ToString();
                    }

                    Console.WriteLine("API Management Template");
                    Console.WriteLine();
                    Console.WriteLine("Connecting to {0} API Management Service on {1} Resource Group ...", sourceApim, resourceGroup);
                    if (singleApiName != null)
                    {
                        Console.WriteLine("Executing extraction for {0} API ...", singleApiName);
                    }
                    else
                    {
                        Console.WriteLine("Executing full extraction ...", singleApiName);
                    }

                    // initialize file helper classes
                    FileWriter fileWriter = new FileWriter();
                    FileNameGenerator fileNameGenerator = new FileNameGenerator();
                    FileNames fileNames = fileNameGenerator.GenerateFileNames(sourceApim);

                    // initialize entity extractor classes
                    APIExtractor apiExtractor = new APIExtractor(fileWriter);
                    APIVersionSetExtractor apiVersionSetExtractor = new APIVersionSetExtractor();
                    AuthorizationServerExtractor authorizationServerExtractor = new AuthorizationServerExtractor();
                    BackendExtractor backendExtractor = new BackendExtractor();
                    LoggerExtractor loggerExtractor = new LoggerExtractor();
                    PolicyExtractor policyExtractor = new PolicyExtractor(fileWriter);
                    PropertyExtractor propertyExtractor = new PropertyExtractor();
                    TagExtractor tagExtractor = new TagExtractor();
                    ProductExtractor productExtractor = new ProductExtractor(fileWriter);
                    MasterTemplateExtractor masterTemplateExtractor = new MasterTemplateExtractor();

                    // extract templates from apim service
                    Template globalServicePolicyTemplate = await policyExtractor.GenerateGlobalServicePolicyTemplateAsync(sourceApim, resourceGroup, policyXMLBaseUrl, fileFolder);
                    Template apiTemplate = await apiExtractor.GenerateAPIsARMTemplateAsync(sourceApim, resourceGroup, singleApiName, policyXMLBaseUrl, fileFolder);
                    List<TemplateResource> apiTemplateResources = apiTemplate.resources.ToList();
                    Template apiVersionSetTemplate = await apiVersionSetExtractor.GenerateAPIVersionSetsARMTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, policyXMLBaseUrl);
                    Template authorizationServerTemplate = await authorizationServerExtractor.GenerateAuthorizationServersARMTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, policyXMLBaseUrl);
                    Template loggerTemplate = await loggerExtractor.GenerateLoggerTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, policyXMLBaseUrl);
                    Template productTemplate = await productExtractor.GenerateProductsARMTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, policyXMLBaseUrl, fileFolder);
                    List<TemplateResource> productTemplateResources = productTemplate.resources.ToList();
                    Template namedValueTemplate = await propertyExtractor.GenerateNamedValuesTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, policyXMLBaseUrl);
                    Template tagTemplate = await tagExtractor.GenerateTagsTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, productTemplateResources, policyXMLBaseUrl);
                    List<TemplateResource> namedValueResources = namedValueTemplate.resources.ToList();
                    Template backendTemplate = await backendExtractor.GenerateBackendsARMTemplateAsync(sourceApim, resourceGroup, singleApiName, apiTemplateResources, namedValueResources, policyXMLBaseUrl);

                    // create parameters file
                    Template templateParameters = masterTemplateExtractor.CreateMasterTemplateParameterValues(destinationApim, linkedBaseUrl, linkedUrlQueryString, policyXMLBaseUrl);

                    // write templates to output file location
                    string apiFileName = fileNameGenerator.GenerateExtractorAPIFileName(singleApiName, sourceApim);
                    fileWriter.WriteJSONToFile(apiTemplate, String.Concat(@fileFolder, apiFileName));
                    fileWriter.WriteJSONToFile(apiVersionSetTemplate, String.Concat(@fileFolder, fileNames.apiVersionSets));
                    fileWriter.WriteJSONToFile(authorizationServerTemplate, String.Concat(@fileFolder, fileNames.authorizationServers));
                    fileWriter.WriteJSONToFile(backendTemplate, String.Concat(@fileFolder, fileNames.backends));
                    fileWriter.WriteJSONToFile(loggerTemplate, String.Concat(@fileFolder, fileNames.loggers));
                    fileWriter.WriteJSONToFile(namedValueTemplate, String.Concat(@fileFolder, fileNames.namedValues));
                    fileWriter.WriteJSONToFile(tagTemplate, String.Concat(@fileFolder, fileNames.tags));
                    fileWriter.WriteJSONToFile(productTemplate, String.Concat(@fileFolder, fileNames.products));
                    fileWriter.WriteJSONToFile(globalServicePolicyTemplate, String.Concat(@fileFolder, fileNames.globalServicePolicy));

                    if (linkedBaseUrl != null)
                    {
                        // create a master template that links to all other templates
                        Template masterTemplate = masterTemplateExtractor.GenerateLinkedMasterTemplate(apiTemplate, globalServicePolicyTemplate, apiVersionSetTemplate, productTemplate, loggerTemplate, backendTemplate, authorizationServerTemplate, namedValueTemplate, fileNames, apiFileName, linkedUrlQueryString, policyXMLBaseUrl);
                        fileWriter.WriteJSONToFile(masterTemplate, String.Concat(@fileFolder, fileNames.linkedMaster));
                    }

                    // write parameters to outputLocation
                    fileWriter.WriteJSONToFile(templateParameters, String.Concat(fileFolder, fileNames.parameters));
                    Console.WriteLine("Templates written to output location");
                    Console.WriteLine("Press any key to exit process:");
#if DEBUG
                    Console.ReadKey();
#endif
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error occured: " + ex.Message);
                    throw;
                }
            });
        }
    }
}
