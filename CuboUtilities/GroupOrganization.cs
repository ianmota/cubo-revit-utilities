using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Group = Autodesk.Revit.DB.Group;

namespace CuboUtilities
{
    [Transaction(TransactionMode.Manual)]
    public class GroupOrganization : IExternalCommand
    {
        public static string NomeAleatorio(int tamanho)
        {   //
            //Resumo:
            //generate a random name

            String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            Random random = new Random();
            String result = new string(
                Enumerable.Repeat(chars, tamanho)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());
            return result;
        }
        public static IList<Group> AllGrps(Document doc,string alvenaria)
        {   //
            //Resumo:
            //select masonry model groups

            IList<Group> allModelsGrps = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .Cast<Group>()
                .ToList();
            IList<Group> allGrps = new List<Group>();
            if (alvenaria == "estrutural")
            {
                allGrps = new List<Group>(from grp in allModelsGrps
                                        from e in grp.GetMemberIds()
                                        .Where(x => doc.GetElement(x)
                                        .GetType().Equals(typeof(Wall))
                                        && !doc.GetElement(x)
                                        .Name.ToString()
                                        .Contains("Vedação"))
                                        select grp);
            }

            else if (alvenaria == "vedacao")
            {
                 allGrps = new List<Group>(from grp in allModelsGrps
                                            from e in grp.GetMemberIds()
                                            .Where(x => doc.GetElement(x)
                                            .Name.ToString()
                                            .Contains("Vedação")
                                            && doc.GetElement(x)
                                            .GetType().Equals(typeof(Wall)))
                                            select grp);

            }

            return (allGrps);
        }
        private static void RandomRenameGroups(IList<Group> groups, Document doc)
        {
            //
            //Resumo:
            //rename all selected groups
           
            foreach (Group grp in groups)
            {
                using (Transaction trans = new Transaction(doc, "Rename Group"))
                {
                    trans.Start();
                    grp.GroupType.Name = NomeAleatorio(11);
                    trans.Commit();
                }
            }
        }
        private static int OrdenatedRenameGroups(IList<Group> groups, XYZ linepoint, Document doc, int nGroup)
        {
            
            foreach (Group grp in groups)
            {
                LocationPoint lPoint = grp.Location as LocationPoint;
                XYZ refPoint = lPoint.Point;

                if (linepoint.ToString() == refPoint.ToString())
                {
                    using (Transaction trans = new Transaction(doc, "Rename Group"))
                    {
                        string grpNome = grp.Name.ToString();
                        if (grpNome.All(char.IsDigit))
                        {
                            continue;
                        }
                        else
                        {
                            trans.Start();

                            grp.GroupType.Name = nGroup.ToString("00");

                            trans.Commit();
                            nGroup += 1;
                        }

                    }
                    break;
                }

            }
            return nGroup;

        }
        private static List<XYZ> LinePoints(IList<XYZ> groupsPoint, string oriented)
        {   
            //
            //Resumo:
            //get all groups in same line
            //Parâmetros de tipo:
            // oriented: [horizontal,vertical]

            IList<double> pointsX = new List<double>();
            IList<double> pointsY = new List<double>();
            List<XYZ> linePoints = new List<XYZ>();

            foreach (XYZ point in groupsPoint)
                {
                    pointsY.Add(point.Y);
                    pointsX.Add(point.X);
                }

            double yMax = pointsY.Max();
            double xMin = pointsX.Min();
            
            if (oriented.Equals("horizontal"))
            {
                linePoints = groupsPoint
                    .Where(x => x.Y .ToString("F")== yMax.ToString("F"))
                    .OrderBy(x => x.X)
                    .ToList();
            }
            else if (oriented.Equals("vertical"))
            {
                linePoints = groupsPoint
                    .Where(x => x.X.ToString("F") == xMin.ToString("F"))
                    .OrderByDescending(p => p.Y)
                    .ToList();
            }
            return linePoints;
            
        }
        private static IList<Group> OrientationGroups(IList<Group> groups, View view, string orientation)
        {   //
            //Resumo:
            //separete the groups by orientation
            //Parâmetros de tipo:
            //orientation: [horizontal, vertical]

            IList<Group> orientedGroups = new List<Group>();
            foreach (Group grp in groups)
            {
                BoundingBoxXYZ bBox = grp.get_BoundingBox(view);

                double distX = bBox.Max.X - bBox.Min.X;
                double distY = bBox.Max.Y - bBox.Min.Y;

                if (orientation == "horizontal")
                {
                    if (distX >= distY)
                    {
                        orientedGroups.Add(grp);
                    }
                }
                else if (orientation == "vertical")
                {
                    if (distY > distX)
                    {
                        orientedGroups.Add(grp);
                    }
                }

            }
            return orientedGroups;
        }
        public static IList<LocationPoint> GroupsLocation(IList<Group> groups)
        {
            //
            //Resumo:
            //get groups locations
            
            IList<LocationPoint> grpLocations = new List<LocationPoint>();
            foreach (Group grp in groups)
            {
                grpLocations.Add(grp.Location as LocationPoint);
            }
            return grpLocations;
        }
        public static IList<XYZ> GroupsPoints(IList<LocationPoint> groupsLocation)
        {
            //
            //Resumo:
            //get groups points origin

            IList<XYZ> groupsPoints = new List<XYZ>();
            foreach (LocationPoint grpLocation in groupsLocation)
            {
                groupsPoints.Add(grpLocation.Point);
            }
            return groupsPoints;
        }
        private static int TotalLines(IList<XYZ> grpPoints,string oriented)
        {
            return grpPoints.Select(point => (oriented == "vertical" ? point.X : point.Y).ToString("F")).Distinct().Count();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //get application and document objets
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            int nGroup = 1;
            foreach(string alvenaria in new List<string> { "estrutural", "vedacao"}){

                if (AllGrps(doc,alvenaria).Count > 0)
                {
                    RandomRenameGroups(AllGrps(doc,alvenaria), doc);
                    IList<string> groupPosition = new List<string> { "horizontal", "vertical" };

                    foreach (string _position in groupPosition)
                    {
                        IList<Group> flatGroups = OrientationGroups(AllGrps(doc,alvenaria), view, _position);
                        IList<LocationPoint> flatGroupsLocation = GroupsLocation(flatGroups);
                        IList<XYZ> flatGroupsPoints = GroupsPoints(flatGroupsLocation);
                        int yTotalLines = TotalLines(flatGroupsPoints,_position);

                        int flatCount = 0;
                        while (flatCount < yTotalLines)
                        {
                            List<XYZ> flatLinePoints = LinePoints(flatGroupsPoints, _position);
                            foreach (XYZ point in flatLinePoints)
                            {
                                flatGroupsPoints.Remove(point);
                                nGroup = OrdenatedRenameGroups(flatGroups, point, doc, nGroup);
                            }
                            flatCount += 1;
                        }
                    }
                    TaskDialog.Show("Result", "Parede do tipo "+alvenaria+" renomeadas!");
                }

            }
            return Result.Succeeded;
        }
    }
}