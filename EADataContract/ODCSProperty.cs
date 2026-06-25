using EAAddinFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSF.UmlToolingFramework.Wrappers.EA;
using YamlDotNet.RepresentationModel;
using TSF_EA = TSF.UmlToolingFramework.Wrappers.EA;

namespace EADataContract
{
    public class ODCSProperty : ODCSElement
    {
        public static string stereotype => profile + "ODCS_Property";
        internal TSF_EA.Attribute modelAttribute => this.modelElement as TSF_EA.Attribute;
        public ODCSProperty(string name) : base(name)
        {
        }
        public ODCSProperty(Element modelElement) : base(modelElement)
        {
        }
        private YamlMappingNode propertyNode => this.node as YamlMappingNode;
        public ODCSProperty(YamlMappingNode node, ODCSObject owner) : base(node, owner)
        {
            this.primaryKey = getBooleanValue("primaryKey");
            this.primaryKeyPosition = getIntValue("primaryKeyPosition");
            this.unique = getBooleanValue("unique");
            this.required = getBooleanValue("required");
            this.criticalDataElement = getBooleanValue("criticalDataElement");
            this.classification = getStringValue("classification");
            this.logicalType = getStringValue("logicalType");
            this.examples = getStringValue("examples");
            //get logicalTypeOptions if present
            YamlMappingNode logicalTypeOptionsNode = null;
            //check for sibling node with key logicalTypeOptions
            if (node.Children.TryGetValue("logicalTypeOptions", out var optionsNode)
                && optionsNode is YamlMappingNode)
            {
                logicalTypeOptionsNode = (YamlMappingNode)optionsNode;
            }
            //if the logical type is array, we check for the items section and get the logicalType from there instead
            if (this.logicalType == "array")
            {
                this.isArray = true;
                if (node.Children.TryGetValue("items", out var itemsNode)
                    && itemsNode is YamlMappingNode itemsMappingNode)
                {
                    this.logicalType = getStringValue("logicalType", itemsMappingNode);
                    this.physicalType = getStringValue("physicalType", itemsMappingNode);
                    this.upperBound = new UnlimitedNatural("*");
                }
            }
        }
        public bool? primaryKey { get; set; }
        public int? primaryKeyPosition { get; set; }
        public bool? unique { get; set; }
        public bool? required { get; set; }
        public bool? criticalDataElement { get; set; }
        public string classification { get; set; }
        public string logicalType { get; set; }
        public string examples { get; set; }
        public bool isArray { get; set; } = false;
        public UnlimitedNatural upperBound { get; set; } = new UnlimitedNatural("1");


        private ODCSLogicalTypeOptions _options = null;
        public ODCSLogicalTypeOptions options
        {
            get
            {
                if (_options == null
                    && this.propertyNode.Children.TryGetValue("logicalTypeOptions", out var optionsNode)
                && optionsNode is YamlMappingNode)
                {
                    _options = new ODCSLogicalTypeOptions((YamlMappingNode)optionsNode, this);
                }
                return _options;
            }
        }


        public override List<ODCSItem> getChildItems()
        {
            var childItems = new List<ODCSItem>();
            if (this.options != null)
            {
                childItems.Add(this.options);
            }
            childItems.AddRange(this.qualityRules);
            return childItems;
        }

        public override void getModelElement(Element context)
        {
            var contextClass = context as Class;
            if (contextClass == null)
            {
                throw new InvalidDataException("ODCS Property must be imported into a class");
            }
            //get existing attribute
            var existingAttribute = contextClass.attributes
                                    .OfType<TSF_EA.Attribute>()
                                    .FirstOrDefault(x => x.name == this.name
                                                    && x.fqStereotype == stereotype);
            if (existingAttribute != null)
            {
                this.modelElement = existingAttribute;
            }
            else
            {
                //create new attribute
                var newAttribute = contextClass.addOwnedElement<TSF_EA.Attribute>(this.name, "string"); //default to string type
                newAttribute.fqStereotype = stereotype;
                newAttribute.save();
                this.modelElement = newAttribute;
            }

        }

        public override void updateModelElement(int position)
        {
            EAOutputLogger.log($"Updating attribute: {this.name}"
               , 0
               , LogTypeEnum.log);
            base.updateModelElement(position);
            this.modelAttribute.position = position;

            this.modelAttribute.isID = this.primaryKey == true;
            this.modelAttribute.addTaggedValue("unique", this.unique?.ToString());
            this.modelAttribute.lower = (uint)(this.required == true ? 1 : 0);
            this.modelAttribute.upper = upperBound;
            this.modelAttribute.addTaggedValue("criticalDataElement", this.criticalDataElement?.ToString());
            this.modelAttribute.addTaggedValue("classification", this.classification);
            this.modelAttribute.addTaggedValue("logicalType", this.logicalType);
            this.modelAttribute.addTaggedValue("examples","<memo>", this.examples);
            this.modelAttribute.addTaggedValue("primaryKeyPosition", this.primaryKeyPosition?.ToString());

            this.modelAttribute.save();
        }

        protected override void loadDataFromModel()
        {
            base.loadDataFromModel();
            this.primaryKey = this.modelAttribute.isID;
            this.unique = this.modelAttribute.getTaggedValue("unique")?.booleanValue;
            this.required = this.modelAttribute.lower > 0;
            this.criticalDataElement = this.modelAttribute.getTaggedValue("criticalDataElement")?.booleanValue;
            this.classification = modelAttribute.getTaggedValue("classification")?.stringValue;
            this.logicalType = modelAttribute.getTaggedValue("logicalType")?.stringValue;
            this.upperBound = (UnlimitedNatural)modelAttribute.upper;
            this.isArray = this.upperBound.isUnlimited;
            this.examples = modelAttribute.getTaggedValue("examples")?.comment;
            //default value for primaryKeyPosition is -1, which we convert to null in that case.
            var primaryKeyPositionValue = modelAttribute.getTaggedValue("primaryKeyPosition")?.integerValue;
            this.primaryKeyPosition = primaryKeyPositionValue == -1 ? null : primaryKeyPositionValue;
        }

        protected override void getChildrenFromModel()
        {
            //check if any of the tagged values for logicaltype options are filled in
            if (ODCSLogicalTypeOptions.hasLogicalTypeOptionsInModel(this.modelAttribute))
            {
                this.children.Add(new ODCSLogicalTypeOptions(this.modelAttribute));
            }
            //Check for relationships
            foreach (var modelRelation in this.modelAttribute.getRelationships<Association>(true, false)
                                            .Where(x => x.hasStereotype(ODCSRelationship.stereotype)))
            {
                var relationship = new ODCSRelationship(modelRelation);
                this.children.Add(relationship);
            }
            //get quality rules
            //first enumeration
            if (this.modelAttribute.type is TSF_EA.Enumeration)
            {
                var qualityRule = new ODCSQuality(this.modelAttribute);
                this.children.Add(qualityRule);
            }
            //then constraints
            foreach (var constraint in this.modelAttribute.constraints
                                        .OfType<TSF_EA.AttributeConstraint>()
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
            this.addKeyValue("primaryKey", this.primaryKey);
            this.addKeyValue("primaryKeyPosition", this.primaryKeyPosition);
            this.addKeyValue("unique", this.unique);
            this.addKeyValue("required", this.required);
            this.addKeyValue("criticalDataElement", this.criticalDataElement);
            this.addKeyValue("classification", this.classification);
            this.addKeyValue("examples", this.examples);
            
            if (this.children.OfType<ODCSLogicalTypeOptions>().FirstOrDefault() is ODCSLogicalTypeOptions options)
            {
                this.addKeyValue("logicalTypeOptions", options.node);
            }
            if (this.children.OfType<ODCSRelationship>().Any())
            {
                //create relationships sequence node and load relationships
                var relationshipsSequenceNode = new YamlSequenceNode();
                this.addKeyValue("relationships", relationshipsSequenceNode);
                foreach (var relationship in this.children.OfType<ODCSRelationship>())
                {
                    relationshipsSequenceNode.Add(relationship.node);
                }
            }
            if(this.isArray)
            {
                this.addKeyValue("logicalType", "array");
                this.addKeyValue("physicalType", "array");
                var itemsNode = new YamlMappingNode();
                this.addKeyValue("items", itemsNode);
                this.addKeyValue("logicalType", this.logicalType, itemsNode);
                this.addKeyValue("physicalType", this.physicalType, itemsNode);
            }
            else
            {
                this.addKeyValue("logicalType", this.logicalType);
                this.addKeyValue("physicalType", this.physicalType);
            }  
        }
    }

}
