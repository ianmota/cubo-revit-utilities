using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Xml.Schema;
using System.Text.RegularExpressions;
using Group = Autodesk.Revit.DB.Group;
using System.Security.Cryptography.X509Certificates;

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

        public static IList<Group> AllGrps(Document doc)
        {   //
            //Resumo:
            //select all document groups

            IList<Group> allGrps = new FilteredElementCollector(doc)
            .OfClass(typeof(Group))
            .Cast<Group>()
            .ToList();

            return (allGrps);
        }

        public static void RandomRenameGroups(IList<Group> groups, Document doc)
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

        public static int OrdenatedRenameGroups(IList<Group> groups, XYZ linepoint, Document doc, int nGroup)
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
                            if (nGroup < 10)
                            {
                                grp.GroupType.Name = "0" + nGroup.ToString();
                            }
                            else if (nGroup >= 10)
                            {
                                grp.GroupType.Name = nGroup.ToString();
                            }
                            trans.Commit();
                            nGroup += 1;
                        }

                    }
                    break;
                }

            }
            return nGroup;

        }

        public static List<XYZ> LinePoints(IList<XYZ> groupsPoint, string oriented)
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

        public static IList<Group> OrientationGroups(IList<Group> groups, View view, string orientation)
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

        public static int TotalLines(IList<XYZ> grpPoints,string oriented)
        {
            //
            //Resumo:
            //counts groups total lines
            int lineTotal = 0;
            List<string> points = new List<string>();

            if (oriented.Equals("vertical"))
            {
                foreach (XYZ _p in grpPoints)
                {
                    points.Add(_p.X.ToString("F"));
                }

            }
            else if (oriented.Equals("horizontal"))
            {
                foreach (XYZ _p in grpPoints)
                {
                    points.Add(_p.Y.ToString("F"));
                }
            }

            HashSet<string> pXWithoutDuplicates = new HashSet<string>(points); 
            lineTotal = pXWithoutDuplicates.Count; 
            
            return lineTotal;
        }
              
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //get application and document objets
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (AllGrps(doc).Count > 0)
            {
                RandomRenameGroups(AllGrps(doc), doc);
                IList<Group> flatGroups = OrientationGroups(AllGrps(doc), view, "horizontal");
                IList<LocationPoint> flatGroupsLocation = GroupsLocation(flatGroups);
                IList<XYZ> flatGroupsPoints = GroupsPoints(flatGroupsLocation);
                int yTotalLines = TotalLines(flatGroupsPoints,"horizontal");

                int flatCount = 0;
                int nGroup = 1;
                while (flatCount < yTotalLines)
                {
                    List<XYZ> flatLinePoints = LinePoints(flatGroupsPoints, "horizontal");
                    foreach (XYZ point in flatLinePoints)
                    {
                        flatGroupsPoints.Remove(point);
                        nGroup = OrdenatedRenameGroups(flatGroups, point, doc, nGroup);
                    }

                    flatCount += 1;
                }
            }

            /*Upright manipulations
            List<XYZ> vPoints = new List<XYZ>();
            IList<LocationPoint> vLocations = new List<LocationPoint>();

            foreach (Group grp in grpsVertical)
            {
                //take all groups points and groups locations
                LocationPoint vLocation = grp.Location as LocationPoint;
                vPoints.Add(vLocation.Point);
                vLocations.Add(grp.Location as LocationPoint);
            }

            List<string> pointsX = new List<string>();
            foreach (XYZ _p in vPoints)
            {
                pointsX.Add(_p.X.ToString("F"));
            }

            HashSet<string> pXWithoutDuplicates = new HashSet<string>(pointsX); //remove duplicates
            int xTotal = pXWithoutDuplicates.Count; //total upright groups

            int vCont = 0;
            while (vCont < xTotal)
            {

                IList<double> points = new List<double>();
                foreach (XYZ point in vPoints)
                {
                    points.Add(point.X);
                }
                double xMin = points.Min();

                List<XYZ> linePoints = vPoints
                    .Where(x => x.X.ToString("F") == xMin.ToString("F"))
                    .OrderByDescending(p => p.Y)
                    .ToList();

                foreach (XYZ linepoint in linePoints)
                {
                    vPoints.Remove(linepoint);
                    foreach (Group grp in grpsVertical)
                    {
                        LocationPoint lPoint = grp.Location as LocationPoint;
                        XYZ refPoint = lPoint.Point;

                        if (linepoint.ToString() == refPoint.ToString())
                        {
                            using (Transaction trans = new Transaction(doc, "Update GroupName (2)"))
                            {
                                string grpNome = grp.Name.ToString();
                                if (grpNome.All(char.IsDigit))
                                {
                                    continue;
                                }
                                else
                                {
                                    trans.Start();
                                    if (nGroup < 10)
                                    {
                                        grp.GroupType.Name = "0" + nGroup.ToString();
                                    }
                                    else if (nGroup >= 10)
                                    {
                                        grp.GroupType.Name = nGroup.ToString();
                                    }
                                    trans.Commit();

                                    nGroup += 1;
                                }

                            }
                            break;
                        }

                    }

                }

                vCont += 1;
            }*/
            TaskDialog.Show("Grupos no Modelo", "Os grupos foram renomeados com sucesso!");

            return Result.Succeeded;

        }
    }
}