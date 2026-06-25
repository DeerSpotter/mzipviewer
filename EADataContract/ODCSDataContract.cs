using EAAddinFramework.Utilities;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TSF.UmlToolingFramework.Wrappers.EA;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace EADataContract
{
    public class ODCSDataContract : ODCSItem
    {
        public static string stereotype => profile + "ODCS_DataContract";
        private static List<string> contractKeyOrder = new List<string>(new string[] { "name", "id", "version", "description","*", "schema" });   
        private YamlMappingNode root => this.node as YamlMappingNode;
        private Package modelPackage => this.modelElement as Package;
        public ODCSDataContract(string filePath, YamlMappingNode node) : base(node, null)
        {
            this.keyOrder = contractKeyOrder;
            this.filePath = filePath;
            this.name = getStringValue("name");
            this.version = getStringValue("version");

            if (root.Children.TryGetValue("schema", out var schemaNode))
            {
                //create schema
                this.schema = new ODCSSchema(schemaNode, this);
            }
        }
        public void saveToFile(ODCSDataContract existingContract)
        {
            //update the name and version and description of the datacontract with the current values
            //replace the schema by this schema
            //then print it to the file

            existingContract.addKeyValue("name", this.name);
            existingContract.addKeyValue("version", this.version);
            existingContract.addKeyValue("description", this.description);
            existingContract.addKeyValue("id", this.id);
            existingContract.addKeyValue("schema", this.schema.node);

            //actually write the file
            File.WriteAllText(existingContract.filePath, existingContract.getYamlString());
        }
        public ODCSDataContract(Package package) : base(package)
        {
            this.loadFromModel();
            this.keyOrder = contractKeyOrder;
        }
        public void importContract(Package package)
        {
            this.importToModel(package, 0);
            EAOutputLogger.log("Importing Relationships", 0, LogTypeEnum.log);
            this.importRelationships(0);
            //do the cleanup
            EAOutputLogger.log("Cleanup...", 0, LogTypeEnum.log);
            this.cleanUp();
        }
        public string filePath { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string description { get; set; }

        public ODCSSchema schema { get; set; }

        public static ODCSDataContract Parse(string filePath)
        {
            var yamlText = File.ReadAllText(filePath);
            var yaml = new YamlStream();
            try
            {
                yaml.Load(new StringReader(yamlText));
            }
            catch (SemanticErrorException ex)
            {
                throw new InvalidDataException($"Error parsing YAML file '{filePath}' at line {ex.Start.Line}: {ex.Message}");
            }
            //check if there is a root node. If not, create an empty one
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode == null)
            {
                yaml.Documents.Add(new YamlDocument(new YamlMappingNode()));
            }
            var root = (YamlMappingNode)yaml.Documents[0]?.RootNode;
            var dataContract = new ODCSDataContract(filePath, root);
            return dataContract;
        }
        public static ODCSDataContract getUserSelectedContract(bool saveAs = false)
        {
            ODCSDataContract dataContract = null;
            string filePath = null;
            if (saveAs)
            {
                //Let the user select a location to save the .yaml file
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "Select a location to save the Datacontract file",
                    Filter = "Datacontract files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
                    FilterIndex = 1,
                    DefaultExt = "yaml",
                };
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = saveFileDialog.FileName;
                    //if the file doesn't exist, create it
                    if (!File.Exists(filePath))
                    {
                        File.Create(filePath).Close();
                    }
                }
            }
            else
            {
                //Let the user select a .yaml file
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Select a Datacontract file",
                    Filter = "Datacontract files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
                    FilterIndex = 1,
                    Multiselect = false,
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
            }
            dataContract = Parse(filePath);
            return dataContract;
        }

        public override void getModelElement(Element context)
        {
            //context must always be a package
            var contextPackage = context as Package;
            if (contextPackage == null)
            {
                contextPackage = context?.getOwner<Package>();
            }
            if (contextPackage == null)
            {
                throw new InvalidDataException("ODCS Data Contract must be imported into a package or element owned by a package");
            }

            //look for package with correct name and stereotype under context package
            var existingPackage = contextPackage.getNestedPackageTree(true)
                                   .OfType<Package>()
                                   .FirstOrDefault(x => x.name == this.name
                                                    && x.fqStereotype == stereotype);
            if (existingPackage != null)
            {
                this.modelElement = existingPackage;
            }
            else
            {
                //create new package
                var newPackage = contextPackage.addOwnedElement<Package>(this.name ?? this.id); //if name not filled in, use ID instead
                newPackage.fqStereotype = stereotype;
                newPackage.save();
                this.modelElement = newPackage;
            }
        }

        public override void updateModelElement(int position)
        {
            this.modelPackage.name = this.name;
            this.modelPackage.version = this.version;
            this.modelPackage.notes = this.description;
            this.modelPackage.addTaggedValue("id", this.id);
            this.modelPackage.save();
        }

        public override List<ODCSItem> getChildItems()
        {
            return new List<ODCSItem>() { this.schema };
        }

        protected override void loadDataFromModel()
        {
            this.name = this.modelPackage.name;
            this.version = this.modelPackage.version;
            this.description = this.modelPackage.notes;
        }

        protected override void getChildrenFromModel()
        {
            this.schema = new ODCSSchema(this.modelPackage);
            this.children.Add(this.schema);
        }

        protected override void loadYamlNode()
        {
            this.node = new YamlMappingNode();
            this.addKeyValue("name", this.name);
            this.addKeyValue("id", this.id);
            this.addKeyValue("version", this.version);
            this.addKeyValue("description", this.description);
            this.addKeyValue("schema", this.schema.node);
        }
        protected void cleanUp()
        {
            //find all enumeration that are identical, and merge them into one.
            var enumerations = this.modelPackage.getOwnedElementWrappers<Enumeration>(string.Empty, true);
            var deletedEnumIds = new HashSet<string>();
            foreach (var enumeration in enumerations)
            {
                //skip enum if it was already deleted
                if (deletedEnumIds.Contains(enumeration.uniqueID)) continue;
                //find identical enums
                var identicalEnumerations = enumerations.Where(x => x.name == enumeration.name
                                                                && x.id != enumeration.id
                                                                && x.ownedLiterals.Select(y => y.name).OrderBy(y => y).SequenceEqual(enumeration.ownedLiterals.Select(y => y.name).OrderBy(y => y)));
                foreach (var identicalEnumeration in identicalEnumerations)
                {
                    EAOutputLogger.log($"Merging enumeration '{identicalEnumeration.name}'", 0, LogTypeEnum.log);
                    //replace all attributes using the identical enumeration with the current enumeration
                    foreach (var attribute in identicalEnumeration.getUsingAttributes())
                    {
                        attribute.type = enumeration;
                        attribute.save();
                    }
                    //add id to deleted enums list
                    deletedEnumIds.Add(identicalEnumeration.uniqueID);
                    //delete the identical enumeration
                    identicalEnumeration.delete();
                }
            }

            //delete any enumerations that are not used by any attributes
            foreach (var enumeration in this.modelPackage.getOwnedElementWrappers<Enumeration>(string.Empty, true).Where(x => x.getUsingAttributes().Count == 0))
            {
                EAOutputLogger.log($"Deleting enumeration '{enumeration.name}'", 0, LogTypeEnum.log);
                enumeration.delete();
            }
        }
    }
}
