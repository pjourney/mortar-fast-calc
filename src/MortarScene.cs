using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WardogsFastCalc {
 // A schematic object view. Heading is exact; barrel tilt is an illustrative
 // mapping of the game's MIL table, never a ballistic or terrain simulation.
 public sealed class MortarScene {
  readonly Viewport3D viewport;
  readonly Canvas labels;
  readonly Border host;
  readonly PerspectiveCamera camera = new PerspectiveCamera();
  readonly Model3DGroup world = new Model3DGroup();
  readonly Model3DGroup instrument = new Model3DGroup();
  readonly Model3DGroup direction = new Model3DGroup();
  double orbit=32, pitch=30, zoom=9.7;
  Point lastMouse;
  bool dragging;
  public double? Bearing { get; private set; }
  public double? ElevationVisual { get; private set; }
  public bool HasTarget { get { return Bearing.HasValue; } }
  public bool IsTopView { get; private set; }
  public double CameraOrbit { get { return orbit; } }
  public double CameraDistance { get { return zoom; } }
  public Vector3D Forward { get; private set; }
  public int GeometryCount { get { return world.Children.Count+instrument.Children.Count+direction.Children.Count; } }
  public MortarScene(Viewport3D view,Canvas overlay,Border surface,Func<ModifierKeys> modifiers=null) {
   var readModifiers=modifiers??(()=>Keyboard.Modifiers);
   viewport=view; labels=overlay; host=surface;
   camera.FieldOfView=39;camera.NearPlaneDistance=.1;camera.FarPlaneDistance=80;
   viewport.Camera=camera;
   world.Children.Add(new AmbientLight(Rgb("#626D79")));
   world.Children.Add(new DirectionalLight(Rgb("#F2F6FF"),new Vector3D(-3,-5,-2)));
   world.Children.Add(new DirectionalLight(Rgb("#7B9CAA"),new Vector3D(3,-1,2)));
   BuildGround();world.Children.Add(instrument);world.Children.Add(direction);
   viewport.Children.Add(new ModelVisual3D { Content=world });
   host.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){host.Focus();lastMouse=e.GetPosition(host);dragging=true;host.CaptureMouse();e.Handled=true;};
   host.MouseLeftButtonUp+=delegate{dragging=false;host.ReleaseMouseCapture();};
   host.LostMouseCapture+=delegate{dragging=false;};
   host.MouseMove+=delegate(object s,MouseEventArgs e){if(!dragging)return;Point p=e.GetPosition(host);Orbit((p.X-lastMouse.X)*.5,(p.Y-lastMouse.Y)*.3);lastMouse=p;};
   host.MouseWheel+=delegate(object s,MouseWheelEventArgs e){Zoom(e.Delta>0?-.5:.5);e.Handled=true;};
   host.KeyDown+=delegate(object s,KeyEventArgs e){if(HandleKey(e.Key,readModifiers()))e.Handled=true;};
   viewport.SizeChanged+=delegate{UpdateCamera();};
   Update(null);UpdateCamera();
  }
  public void Update(Solution solution) {
   Bearing=solution==null?(double?)null:solution.Bearing;
   ElevationVisual=solution!=null&&solution.Mil.HasValue ? 35+(solution.Mil.Value-150)/700*45 : (double?)null;
   double heading=Bearing??0, elevation=ElevationVisual??65;
   Forward=new Vector3D(Math.Sin(heading*Math.PI/180),0,-Math.Cos(heading*Math.PI/180));
   instrument.Children.Clear();direction.Children.Clear();
   BuildMortar(elevation,ElevationVisual.HasValue);
   instrument.Transform=new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,1,0),-heading));
   if(Bearing.HasValue) {
    string accent=solution.InRange?"#99DDE5":"#E5AF79";
    // The dashed ground ray expresses bearing only. Its length is deliberately schematic.
    for(double d=1.2;d<2.85;d+=.23) Rod(direction,new Point3D(Forward.X*d,.05,Forward.Z*d),new Point3D(Forward.X*(d+.12),.05,Forward.Z*(d+.12)),.014,accent);
    Point3D target=new Point3D(Forward.X*2.9,.07,Forward.Z*2.9);
    Ring(direction,target,.20,.025,accent);
    Rod(direction,new Point3D(target.X-.28,.07,target.Z),new Point3D(target.X+.28,.07,target.Z),.013,accent);
    Rod(direction,new Point3D(target.X,.07,target.Z-.28),new Point3D(target.X,.07,target.Z+.28),.013,accent);
    Rod(direction,target,new Point3D(target.X,.42,target.Z),.016,accent);
   }
   UpdateLabels();
  }
  void BuildGround() {
   Ring(world,new Point3D(0,-.02,0),3,.009,"#41484F");
   Ring(world,new Point3D(0,-.02,0),2,.006,"#292E34");
   Ring(world,new Point3D(0,-.02,0),1,.005,"#292E34");
   for(int i=0;i<72;i++) {
    double r=i*Math.PI/36,inner=i%18==0?2.78:i%6==0?2.87:2.94;
    Rod(world,new Point3D(Math.Sin(r)*inner,0,-Math.Cos(r)*inner),new Point3D(Math.Sin(r)*3,0,-Math.Cos(r)*3),i%18==0?.016:.006,i%18==0?"#929CA6":"#46505A");
   }
   for(int i=-3;i<=3;i++) {
    double end=Math.Sqrt(9-i*i);
    Rod(world,new Point3D(i,-.04,-end),new Point3D(i,-.04,end),.004,"#252A30");
    Rod(world,new Point3D(-end,-.04,i),new Point3D(end,-.04,i),.004,"#252A30");
   }
   Cylinder(world,new Point3D(0,-.10,0),new Point3D(0,-.08,0),.93,.93,"#090B0E",64);
  }
  void BuildMortar(double elevation,bool valid) {
   string metal=valid?"#ACB5BF":"#757D86";
   Cylinder(instrument,new Point3D(0,.02,0),new Point3D(0,.17,0),.73,.63,"#7C858F",48);
   Cylinder(instrument,new Point3D(0,.17,0),new Point3D(0,.28,0),.24,.16,"#B4BDC5",32);
   for(int i=0;i<8;i++){double r=i*Math.PI/4;Rod(instrument,new Point3D(Math.Cos(r)*.25,.18,Math.Sin(r)*.25),new Point3D(Math.Cos(r)*.63,.15,Math.Sin(r)*.63),.026,"#AEB7C0");}
   double a=elevation*Math.PI/180;
   Vector3D axis=new Vector3D(0,Math.Sin(a),-Math.Cos(a));
   Point3D pivot=new Point3D(0,.28,0),muzzle=pivot+axis*2.5;
   Tube(instrument,pivot,muzzle,.155,.108,metal);
   Cylinder(instrument,pivot+axis*.03,pivot+axis*.36,.185,.18,"#929BA5",32);
   Tube(instrument,pivot+axis*2.36,pivot+axis*2.52,.179,.108,"#E6EBEF");
   Point3D collar=pivot+axis*1.05;
   Cylinder(instrument,collar-axis*.08,collar+axis*.09,.195,.195,"#636F7A",32);
   Point3D bridge=collar+new Vector3D(0,-.24,-.15);
   Rod(instrument,collar,bridge,.075,"#B5BFC8");
   foreach(int sign in new[]{-1,1}) {
    Point3D foot=new Point3D(sign*.73,.06,-.83);
    Rod(instrument,bridge,foot,.052,"#C5CFD8");
    Rod(instrument,new Point3D(sign*.70,.065,-.62),new Point3D(sign*.76,.065,-1.04),.055,"#66727E");
    Rod(instrument,new Point3D(0,.18,-.15),foot,.022,"#737E88");
   }
   Rod(instrument,collar+new Vector3D(-.33,0,0),collar+new Vector3D(.33,0,0),.027,"#B8C2CB");
   Cylinder(instrument,collar+new Vector3D(.33,0,0),collar+new Vector3D(.39,0,0),.105,.105,"#667480",24);
   Rod(instrument,collar+new Vector3D(-.20,.02,0),collar+new Vector3D(-.33,.31,0),.025,"#AAB5BF");
   Cylinder(instrument,collar+new Vector3D(-.33,.31,.11),collar+new Vector3D(-.33,.31,-.16),.052,.052,"#6D7B86",20);
  }
  public void ResetCamera(){orbit=32;pitch=30;zoom=9.7;IsTopView=false;UpdateCamera();}
  public void ToggleTop(){IsTopView=!IsTopView;UpdateCamera();}
  public void Orbit(double horizontal,double vertical){IsTopView=false;orbit=(orbit+horizontal+360)%360;pitch=Math.Max(12,Math.Min(75,pitch+vertical));UpdateCamera();}
  public void Zoom(double delta){zoom=Math.Max(7,Math.Min(14,zoom+delta));UpdateCamera();}
  public bool HandleKey(Key key){return HandleKey(key,ModifierKeys.None);}
  public bool HandleKey(Key key,ModifierKeys modifiers){
   // On the main keyboard, '+' is Shift+OemPlus; retain unmodified
   // equals and numpad support while leaving other shortcuts alone.
   if(modifiers!=ModifierKeys.None&&!(modifiers==ModifierKeys.Shift&&key==Key.OemPlus))return false;
   switch(key){case Key.Left:Orbit(-10,0);break;case Key.Right:Orbit(10,0);break;case Key.Up:Orbit(0,5);break;case Key.Down:Orbit(0,-5);break;
    case Key.Home:ResetCamera();break;case Key.Space:ToggleTop();break;case Key.Add:case Key.OemPlus:Zoom(-.5);break;case Key.Subtract:case Key.OemMinus:Zoom(.5);break;default:return false;}return true;
  }
  void UpdateCamera() {
   // Keep vertical framing stable when the application window gets wider.
   double aspect=viewport.ActualHeight>0?viewport.ActualWidth/viewport.ActualHeight:1.8;
   camera.FieldOfView=2*Math.Atan(Math.Tan(15*Math.PI/180)*aspect)*180/Math.PI;
   double a=orbit*Math.PI/180,p=pitch*Math.PI/180;
   Point3D look=new Point3D(0,.55,0);
   camera.Position=IsTopView?new Point3D(0,zoom*1.4,.001):look+new Vector3D(Math.Sin(a)*Math.Cos(p)*zoom,Math.Sin(p)*zoom,Math.Cos(a)*Math.Cos(p)*zoom);
   camera.LookDirection=look-camera.Position;camera.UpDirection=IsTopView?new Vector3D(0,0,-1):new Vector3D(0,1,0);
   UpdateLabels();
  }
  void UpdateLabels() {
   labels.Children.Clear();if(viewport.ActualWidth<1||viewport.ActualHeight<1)return;
   Label("N",new Point3D(0,.05,-3.35),"#E8EDF2",12);
   Label("E",new Point3D(3.35,.05,0),"#89929B",11);
   Label("S",new Point3D(0,.05,3.35),"#89929B",11);
   Label("W",new Point3D(-3.35,.05,0),"#89929B",11);
   if(Bearing.HasValue)Label("TARGET",new Point3D(Forward.X*2.9,.85,Forward.Z*2.9),ElevationVisual.HasValue?"#A3E1E8":"#E5AF79",10,IsTopView?-18:0);
  }
  void Label(string value,Point3D position,string color,double size,double yOffset=0){
   Vector3D forward=camera.LookDirection;forward.Normalize();Vector3D right=Vector3D.CrossProduct(forward,camera.UpDirection);right.Normalize();Vector3D up=Vector3D.CrossProduct(right,forward);up.Normalize();
   Vector3D offset=position-camera.Position;double depth=Vector3D.DotProduct(offset,forward);if(depth<=0)return;
   double scale=viewport.ActualWidth/(2*Math.Tan(camera.FieldOfView*Math.PI/360)*depth);
   double x=viewport.ActualWidth/2+Vector3D.DotProduct(offset,right)*scale,y=viewport.ActualHeight/2-Vector3D.DotProduct(offset,up)*scale;
   var text=new TextBlock{Text=value,FontSize=size,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Rgb(color))};text.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
   Canvas.SetLeft(text,x-text.DesiredSize.Width/2);Canvas.SetTop(text,y-text.DesiredSize.Height/2+yOffset);labels.Children.Add(text);
  }
  static Color Rgb(string hex){return (Color)ColorConverter.ConvertFromString(hex);}
  static Material Material(string hex){var group=new MaterialGroup();group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Rgb(hex))));group.Children.Add(new SpecularMaterial(new SolidColorBrush(Rgb("#82919F")),45));group.Freeze();return group;}
  static void Mesh(Model3DGroup group,MeshGeometry3D mesh,string color){mesh.Freeze();var mat=Material(color);var model=new GeometryModel3D(mesh,mat){BackMaterial=mat};model.Freeze();group.Children.Add(model);}
  static void Basis(Point3D a,Point3D b,out Vector3D u,out Vector3D v){Vector3D axis=b-a;axis.Normalize();u=Vector3D.CrossProduct(axis,Math.Abs(axis.Y)>.95?new Vector3D(1,0,0):new Vector3D(0,1,0));u.Normalize();v=Vector3D.CrossProduct(axis,u);}
  static void Quad(MeshGeometry3D mesh,Point3D a,Point3D b,Point3D c,Point3D d){int i=mesh.Positions.Count;mesh.Positions.Add(a);mesh.Positions.Add(b);mesh.Positions.Add(c);mesh.Positions.Add(d);foreach(int n in new[]{0,1,2,0,2,3})mesh.TriangleIndices.Add(i+n);}
  static void Rod(Model3DGroup g,Point3D a,Point3D b,double radius,string color){if((b-a).Length<.0001)return;Cylinder(g,a,b,radius,radius,color,8);}
  static void Cylinder(Model3DGroup g,Point3D a,Point3D b,double ra,double rb,string color,int steps){
   Vector3D u,v;Basis(a,b,out u,out v);var m=new MeshGeometry3D();
   for(int i=0;i<steps;i++){double p=i*2*Math.PI/steps,q=(i+1)*2*Math.PI/steps;Vector3D d=u*Math.Cos(p)+v*Math.Sin(p),e=u*Math.Cos(q)+v*Math.Sin(q);Quad(m,a+d*ra,a+e*ra,b+e*rb,b+d*rb);Quad(m,a,a+e*ra,a+d*ra,a);Quad(m,b,b+d*rb,b+e*rb,b);}
   Mesh(g,m,color);
  }
  static void Tube(Model3DGroup g,Point3D a,Point3D b,double outer,double inner,string color){
   Vector3D u,v;Basis(a,b,out u,out v);var shell=new MeshGeometry3D();var inside=new MeshGeometry3D();
   for(int i=0;i<48;i++){double p=i*2*Math.PI/48,q=(i+1)*2*Math.PI/48;Vector3D d=u*Math.Cos(p)+v*Math.Sin(p),e=u*Math.Cos(q)+v*Math.Sin(q);Quad(shell,a+d*outer,a+e*outer,b+e*outer,b+d*outer);Quad(shell,b+d*outer,b+e*outer,b+e*inner,b+d*inner);Quad(inside,a+d*inner,b+d*inner,b+e*inner,a+e*inner);}
   Mesh(g,shell,color);Mesh(g,inside,"#151B21");
  }
  static void Ring(Model3DGroup g,Point3D center,double radius,double width,string color){
   var m=new MeshGeometry3D();for(int i=0;i<96;i++){double p=i*2*Math.PI/96,q=(i+1)*2*Math.PI/96;Vector3D d=new Vector3D(Math.Cos(p),0,Math.Sin(p)),e=new Vector3D(Math.Cos(q),0,Math.Sin(q));Quad(m,center+d*(radius-width),center+e*(radius-width),center+e*(radius+width),center+d*(radius+width));}Mesh(g,m,color);
  }
 }
}
