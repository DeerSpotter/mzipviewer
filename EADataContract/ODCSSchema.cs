using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;
using TSF_EA = TSF.UmlToolingFramework.Wrappers.EA;

namespace EADataContract
{
    public class ODCSSchema : ODCSItem
    {
        
        public ODCSSchema(YamlNode node, ODCSDataContract owner) : base(node, owner)
        {
        }
        public ODCSSchema(TSF_EA.Package package) : base(package)
        {
        }
        protected YamlSequenceNode sequenceNode => (YamlSequenceNode)node;
        private List<ODCSObject> _objects = null;
        public List<ODCSObject> objects
        {
            get
            {
                if (_objects == null)
                {
                    var sequenceNode = this.node as YamlSequenceNode;
                    if (sequenceNode == null) { return null; }

                    this._objects = new List<ODCSObject>();
                    foreach (var objectNode in sequenceNode.Children.OfType<YamlMappingNode>())
                    {
                        var odcsObject = new ODCSObject(objectNode, this);
                        this._objects.Add(odcsObject);
                    }
                }
                return _objects;
            }
        }

        public override void getModelElement(TSF_EA.Element context)
        {
            this.modelElement = context;
        }

        public override void updateModelElement(int position)
        {
            return; //schema has no model element to update
        }

        public override List<ODCSItem> getChildItems()
        {
            return this.objects.OfType<ODCSItem>().ToList();
        }

        protected override void loadDataFromModel()
        {
            //no specific data to be loaded for the schema
        }

        protected override void getChildrenFromModel()
        {
            //get all objects in the package
            foreach (var classElement in this.modelElement
                                .ownedElements.OfType<TSF_EA.Class>()
                                .Where(x => x.hasStereotype(ODCSObject.stereotype)))
            {
                var odcsObject = new ODCSObject(classElement);
                this.children.Add(odcsObject);
            }
        }

        protected override void loadYamlNode()
        {
            this.node = new YamlSequenceNode();
            foreach (var child in this.children)
            {
                this.sequenceNode.Add(child.node);
            }
        }
    }
}