using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSF.UmlToolingFramework.Wrappers.EA;
using YamlDotNet.RepresentationModel;

namespace EADataContract
{
    public abstract class ODCSElement: ODCSItem
    {
        protected YamlMappingNode mappingNode => (YamlMappingNode)node;
        protected ODCSElement(Element modelElement) : base(modelElement) { }
        public ODCSElement(string name) 
        { 
            this.name = name;
        }
        public ODCSElement(YamlMappingNode node, ODCSItem owner) :base(node, owner)
        {
            this.name = getStringValue("name");
            
            this.physicalName = getStringValue("physicalName");
            this.physicalType = getStringValue("physicalType");
            this.description = getStringValue("description");
            this.businessName = getStringValue("businessName");
            this.authoritativeDefinitions = getStringValue("authoritativeDefinitions");
            this.tags = getStringValue("tags");
            this.customProperties = getStringValue("customProperties");
            if (node.Children.TryGetValue("quality", out var qualityNode)
                && qualityNode is YamlSequenceNode)
            {
                var qualitySeqNode = (YamlSequenceNode)qualityNode;
                foreach (var child in qualitySeqNode.Children.OfType<YamlMappingNode>())
                {
                    this._qualityRules.Add(new ODCSQuality(child, this));
                }
            }
        }
        public override void updateModelElement(int position)
        {
            this.modelElement.name = this.name;
            this.modelElement.notes = this.description;
            this.modelElement.addTaggedValue("id", this.id);
            this.modelElement.addTaggedValue("physicalName", this.physicalName);
            this.modelElement.addTaggedValue("physicalType", this.physicalType);
            this.modelElement.addTaggedValue("businessName", this.businessName);
            this.modelElement.addTaggedValue("authoritativeDefinitions","<memo>" , this.authoritativeDefinitions);
            this.modelElement.addTaggedValue("tags", "<memo>", this.tags);
            this.modelElement.addTaggedValue("customProperties", "<memo>", this.customProperties);
        }
        public string name { get; set; }
        public string physicalName { get; set; }
        public string physicalType { get; set; }
        public string description { get; set; }
        public string businessName { get; set; }
        public string authoritativeDefinitions { get; set; }
        public string tags { get; set; }
        public string customProperties { get; set; }

        private List<ODCSQuality> _qualityRules = new List<ODCSQuality>();
        public IEnumerable<ODCSQuality> qualityRules => this._qualityRules;

        protected override void loadDataFromModel()
        {             
            this.name = this.modelElement.name;
            this.description = this.modelElement.notes;
            this.id = this.modelElement.getTaggedValue("id")?.stringValue;
            this.physicalName = this.modelElement.getTaggedValue("physicalName")?.stringValue;
            this.physicalType = this.modelElement.getTaggedValue("physicalType")?.stringValue;
            this.businessName = this.modelElement.getTaggedValue("businessName")?.stringValue;
            this.authoritativeDefinitions = this.modelElement.getTaggedValue("authoritativeDefinitions")?.comment;
            this.tags = this.modelElement.getTaggedValue("tags")?.comment;
            this.customProperties = this.modelElement.getTaggedValue("customProperties")?.comment;
        }

        protected override void loadYamlNode()
        {
            this.node = new YamlMappingNode();
            this.addKeyValue("name", this.name);
            this.addKeyValue("id", this.id);
            this.addKeyValue("physicalName", this.physicalName);
            this.addKeyValue("physicalType", this.physicalType);
            this.addKeyValue("description", this.description);
            this.addKeyValue("businessName", this.businessName);
            this.addKeyValue("authoritativeDefinitions", this.authoritativeDefinitions);
            this.addKeyValue("tags", this.tags);
            this.addKeyValue("customProperties", this.customProperties);
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
        }
    }
}
