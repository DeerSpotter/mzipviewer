using EAAddinFramework.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TSF.UmlToolingFramework.Wrappers.EA;
using YamlDotNet.RepresentationModel;

namespace EADataContract
{
    public class ODCSObject: ODCSElement
    {
        public static string stereotype => profile + "ODCS_Object";
        private string dataGranularityDescription { get;set; }

        public ODCSObject(Element modelElement) : base(modelElement) { }
        public ODCSObject(YamlMappingNode node, ODCSSchema owner) : base(node, owner)
        {
            this.dataGranularityDescription = getStringValue("dataGranularityDescription");
        }
        private Class modelClass => this.modelElement as Class;
        private List<ODCSProperty> _properties = null;
        public IEnumerable<ODCSProperty> properties
        {
            get
            {
                if (_properties == null)
                {
                    if (mappingNode == null) { return null; }
                    if (!mappingNode.Children.TryGetValue("properties", out var propertiesNode))
                    {
                        return null;
                    }
                    var propertiesSequenceNode = propertiesNode as YamlSequenceNode;
                    if (propertiesSequenceNode == null) { return null; }
                    this._properties = new List<ODCSProperty>();
                    foreach (var propertyNode in propertiesSequenceNode.Children.OfType<YamlMappingNode>())
                    {
                        var odcsProperty = new ODCSProperty(propertyNode, this);
                        this._properties.Add(odcsProperty);
                    }
                }
                return _properties;
            }
        }

        public override void getModelElement(Element context)
        {
            var contextWrapper = context as ElementWrapper;
            if (contextWrapper == null)
            {
                throw new InvalidDataException("ODCS Object must be imported into an package");
            }

            //get existing element
            var existingElement = context.ownedElements
            .OfType<Class>()
            .FirstOrDefault(x => x.name == this.name 
                            && x.fqStereotype == stereotype);
            if (existingElement != null)
            {
                this.modelElement = existingElement;
            }
            else 
            {
                //create new element
                var newElement = contextWrapper.addOwnedElement<Class>(this.name);
                newElement.fqStereotype = stereotype;
                newElement.save();
                this.modelElement = newElement;
            }
        }

        public override void updateModelElement(int position)
        {
            EAOutputLogger.log( $"Updating object: {this.name}"
                   , this.modelClass.id
                  , LogTypeEnum.log);
            base.updateModelElement(position);
            this.modelClass.position = position;
            this.modelClass.addTaggedValue("dataGranularityDescription", this.dataGranularityDescription);
            //TODO : quality
            this.modelClass.save();
        }

        public override List<ODCSItem> getChildItems()
        {
            var childItems = new List<ODCSItem>(this.properties);
            childItems.AddRange(this.qualityRules);
            return childItems;
        }

        protected override void loadDataFromModel()
        {
            base.loadDataFromModel();
            this.dataGranularityDescription = this.modelClass.getTaggedValue("dataGranularityDescription")?.stringValue;
        }

        protected override void getChildrenFromModel()
        {
            foreach (var attribute in this.modelClass.attributes.OfType<Attribute>()
                                .Where(attr => attr.hasStereotype(ODCSProperty.stereotype)))
            {
                var odcsProperty = new ODCSProperty(attribute);
                this.children.Add(odcsProperty);
            }
            //then constraints
            foreach (var constraint in this.modelClass.constraints
                                        .OfType<Constraint>()
                                        .Where(x => x.specification is OpaqueExpression spec
                                                && spec.languages.Contains("Quality")))
            {
                var qualityRule = new ODCSQuality(constraint);
                this.children.Add(qualityRule);
            }
        }

        protected override void loadYamlNode()
        {
            base.loadYamlNode();
            this.addKeyValue("dataGranularityDescription", this.dataGranularityDescription);
            //add quality rules
            if (this.children.OfType<ODCSQuality>().Any())
            {
                //create quality sequence node and load quality rules
                var qualitySequenceNode = new YamlSequenceNode();
                this.addKeyValue("quality", qualitySequenceNode);
                foreach (var qualityRule in this.children.OfType<ODCSQuality>()
                        .Where(x => x.node != null))
                {
                    qualitySequenceNode.Add(qualityRule.node);
                }
            }
            //create properties sequence node and load properties
            var propertiesSequenceNode = new YamlSequenceNode();
            this.addKeyValue("properties", propertiesSequenceNode);
            foreach (var property in this.children.OfType<ODCSProperty>())
            {
                propertiesSequenceNode.Add(property.node);
            }

        }
    }
}
