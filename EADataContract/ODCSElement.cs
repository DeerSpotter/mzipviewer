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
        }
        public string name { get; set; }
        public string physicalName { get; set; }
        public string physicalType { get; set; }
        public string description { get; set; }
        public string businessName { get; set; }

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


        }

    }
}
