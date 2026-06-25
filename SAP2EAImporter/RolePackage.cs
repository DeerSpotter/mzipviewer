using EAAddinFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UML = TSF.UmlToolingFramework.UML;
using UMLEA = TSF.UmlToolingFramework.Wrappers.EA;

namespace SAP2EAImporter
{
    class RolePackage : SAPElement<UMLEA.Class>
    {
        public static string stereotype => "SAP_rolePackage";
        const string outputName = "SAP2EAImporter"; //TODO move to settings
        public RolePackage(string name, UML.Classes.Kernel.Package package)
            : base(name, package, stereotype) { }
        public RolePackage(UMLEA.Class element) : base(element) { }
        public void addRole(Role role)
        {
            // Check if the aggregation relationship exists already.
            if (!this.wrappedElement.relationships
                .OfType<UMLEA.ConnectorWrapper>()
                .Any(x => x.stereotypes.Any(y => y.name == "SAP_aggregation")
                    && x.target.name == role.name))
            {
                //If the relationship does not exist, create one
                var newRelation = this.wrappedElement.model.factory.createNewElement<UMLEA.Association>(this.wrappedElement, "");
                newRelation.addStereotype("SAP_aggregation");
                newRelation.target = role.wrappedElement;
                newRelation.sourceEnd.aggregation = UML.Classes.Kernel.AggregationKind.shared;
                newRelation.save();
            }

        }
        public void addRole(string RoleName)
        {
            // Check if the aggregation relationship exists already.
            if (!this.wrappedElement.relationships
                .OfType<UMLEA.ConnectorWrapper>()
                .Any(x => x.stereotypes.Any(y => y.name == "SAP_aggregation")
                    && x.target.name == RoleName))
            {
                //If the relationship does not exist, create one
                var sqlGetData = $"select top(1) o.Object_ID from t_object o where o.name = '{RoleName}' and  o.stereotype in ('SAP_singleRole', 'SAP_compositeRole')";
                var roleElement = this.wrappedElement.EAModel.getElementWrappersByQuery(sqlGetData).FirstOrDefault();
                if (roleElement != null)
                {
                    var newRelation = this.wrappedElement.model.factory.createNewElement<UMLEA.Association>(this.wrappedElement, "");
                    newRelation.addStereotype("SAP_aggregation");
                    newRelation.target = roleElement;
                    newRelation.sourceEnd.aggregation = UML.Classes.Kernel.AggregationKind.shared;
                    newRelation.save();
                }
                else
                {
                    EAOutputLogger.log(this.wrappedElement.EAModel, outputName
                            , $"Cannot find role with name '{RoleName}' in the model'"
                            , wrappedElement.id
                            , LogTypeEnum.error);
                }
            }
        }
    }
}

