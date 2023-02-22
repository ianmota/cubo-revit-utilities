using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Security;
using System.ComponentModel;
using Autodesk.Revit.DB.Analysis;

namespace CuboUtilities
{
    [Transaction(TransactionMode.Manual)]
    public class ElevationCreation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;

            IList<ViewFamilyType> viewsFamilyTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .ToList<ViewFamilyType>();

            ViewFamilyType Elevation = null;
            foreach (ViewFamilyType type in viewsFamilyTypes)
            {
                if (type.Name.ToString() == "Elevação da construção")
                {
                    Elevation = type;
                }

            }

            IList<ElementId> grpsID = new List<ElementId>();
            foreach(string alvenaria in new List<string> { "estrutural", "vedacao" })
            {
                foreach (Group grp in GroupOrganization.AllGrps(doc, alvenaria))
                {
                    grpsID = grp.GetMemberIds();

                    //Melhor fazer um filtro depois, consome menos memória
                    foreach (ElementId grpID in grpsID)
                    {
                       Element element = doc.GetElement(grpID);

                        if (element.GetType().Equals(typeof(Wall))) 
                        {
                            //Verificar boundingbox de paredes unidas com outras
                            //não está como o esperado
                            Wall wallGrp = (Wall)element;
                            LocationCurve locationCurve = wallGrp.Location as LocationCurve;
                            Line wallLine = locationCurve.Curve as Line;
                            BoundingBoxXYZ wallBB = wallGrp.get_BoundingBox(view);
                            XYZ wallOrigin = wallLine.Origin;
                            XYZ wallDirection = wallLine.Direction;
                            double xOrigin = new double();
                            double yOrigin = new double();
                            int nIndex = new int();
                            if (wallDirection.ToString() == XYZ.BasisX.ToString())
                            {
                                nIndex = 1;
                                xOrigin = (Math.Abs(wallBB.Max.X) + Math.Abs(wallBB.Min.X)) / 2;
                                yOrigin = wallBB.Min.Y;
                            }
                            else if (wallDirection.ToString() == XYZ.BasisY.ToString())
                            {
                                nIndex = 0;
                                yOrigin = (Math.Abs(wallBB.Max.Y) + Math.Abs(wallBB.Min.Y)) / 2;
                                xOrigin = wallBB.Min.X;
                            }

                            XYZ elevationOrigin = new XYZ(xOrigin, yOrigin, 0);
                        
                            //Criar elevação
                            using (Transaction trans = new Transaction(doc, "Criar elevação"))
                            {
                                trans.Start();
                                ElevationMarker marker = ElevationMarker.CreateElevationMarker(doc, Elevation.Id,
                                    elevationOrigin, view.Scale);
                                marker.CreateElevation(doc, view.Id, nIndex).Name = grp.GroupType.Name
                                    .ToString();
                                trans.Commit();
                            }

                            TaskDialog.Show("Elevação Status", "Elevações Criadas!");
                        }
                   

                    }
                }

            }

            return Result.Succeeded;
        }
    }
}
