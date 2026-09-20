using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Xml;

namespace WardogsFastCalc {
 public sealed class SavedTarget {
  public string Origin, Target, Distance, Summary;
  public override string ToString() { return Summary; }
 }
 public sealed class Controller {
  public Window Window;
  public TextBox Origin, Target, Distance;
  public ListBox History;
  public Solution Current;
  public MortarScene Scene;
  public readonly List<SavedTarget> Saved = new List<SavedTarget>();
  bool loading;
  readonly string statePath;
  public Controller(string path) {
   statePath = path;
   using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) Window = (Window)XamlReader.Load(stream);
   using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico")) {
    Window.Icon = BitmapFrame.Create(icon,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
   }
   Origin = Find<TextBox>("Origin"); Target = Find<TextBox>("Target"); Distance = Find<TextBox>("Distance"); History = Find<ListBox>("History");
   Scene=new MortarScene(Find<Viewport3D>("SceneViewport"),Find<Canvas>("SceneLabels"),Find<Border>("SceneHost"));
   Find<Button>("ViewTop").Click+=delegate{Scene.ToggleTop();};
   Find<Button>("ViewReset").Click+=delegate{Scene.ResetCamera();};
   Window.SizeChanged+=delegate{Find<Border>("SceneHost").Height=Math.Max(180,Math.Min(300,Window.ActualHeight-580));};
   Origin.TextChanged += Changed; Target.TextChanged += Changed; Distance.TextChanged += Changed;
   foreach (var box in new[] {Origin, Target, Distance}) box.GotKeyboardFocus += delegate(object s, KeyboardFocusChangedEventArgs e) { ((TextBox)s).SelectAll(); };
   Find<Button>("Save").Click += delegate { SaveTarget(); };
   Find<Button>("Copy").Click += delegate { CopyCallout(); };
   Find<Button>("NewTarget").Click += delegate { NewTarget(); };
   Find<Button>("Help").Click += delegate { ShowHelp(); };
   Find<Button>("Example").Click += delegate { LoadExample(); };
   Find<CheckBox>("Pin").Checked += delegate { Window.Topmost = true; };
   Find<CheckBox>("Pin").Unchecked += delegate { Window.Topmost = false; };
   History.MouseDoubleClick += delegate { Recall(); };
   Window.PreviewKeyDown += OnKey;
   Window.Closing += delegate { Persist(); };
   Window.Loaded += delegate { Focus(Origin); };
   LoadState(); Update();
  }
  public T Find<T>(string name) where T : class { return Window.FindName(name) as T; }
  static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
  void Set(string name, string text) { Find<TextBlock>(name).Text = text; }
  void Changed(object sender, TextChangedEventArgs e) { if (!loading) Update(); }
  public void Focus(TextBox box) { box.Focus(); box.SelectAll(); }
  public void SetInputs(string a, string b, string distance) {
   loading=true; Origin.Text=a; Target.Text=b; Distance.Text=distance; loading=false; Update();
  }
  public void LoadExample() { SetInputs("x98.43, y110.38", "x94.53, y109.03", ""); Focus(Target); }
  public void NewTarget() { SetInputs(Origin.Text,"",""); Focus(Target); }
  public void Update() {
   Current=null;
   Coordinate a,b; double number; double? distance=null;
   string error=null;
   if (!Calculator.TryCoordinate(Origin.Text, out a)) error=String.IsNullOrWhiteSpace(Origin.Text) ? "Enter your mortar coordinates. Example: x98.43, y110.38" : "Your mortar: enter one X/Y pair, each from 0 to 164.";
   if (!Calculator.TryCoordinate(Target.Text, out b) && error==null) error=String.IsNullOrWhiteSpace(Target.Text) ? "Enter target coordinates to get a bearing and range." : "Target: enter one X/Y pair, each from 0 to 164.";
   if (error==null && !String.IsNullOrWhiteSpace(Distance.Text)) {
    if (!Calculator.TryNumber(Distance.Text.Trim(),out number) || number<=0 || number>25000) error="Distance: use a number above 0, up to 25000 meters.";
    else distance=number;
   }
   if (error==null) { try { Current=Calculator.Solve(a,b,distance); } catch (ArgumentException ex) { error=ex.Message; } }
   Find<Button>("Save").IsEnabled=Current!=null;
   Find<Button>("Copy").IsEnabled=Current!=null;
   if (Current==null) {
    Set("InputStatus",error); Set("RangeStatus","WAITING FOR VALID COORDINATES");
    Find<TextBlock>("RangeStatus").Foreground=Color("#A7B5BE");
    Set("Bearing","—"); Set("Range","—"); Set("Mil","— MIL");
    Set("Direction","North 0° · East 90°"); Set("RangeDetail","meters");
    Set("Instruction","Paste your mortar and target coordinates. Then match the compass bearing and the left-side RNG readout in the game.");
    Scene.Update(null);Set("SceneState","Enter coordinates to align");return;
   }
   var s=Current;
   Set("InputStatus", s.Overridden ? String.Format(CultureInfo.InvariantCulture,"OVERRIDE ACTIVE · grid distance {0:0.0} m. Clear distance to return to automatic range.",s.Distance) : "Using coordinate distance · updates instantly.");
   Set("Bearing",(Math.Round(s.Bearing,1)%360).ToString("000.0",CultureInfo.InvariantCulture)+"°");
   Set("Direction",s.Direction+" · compass heading");
   Set("Range",s.Range.ToString("0",CultureInfo.InvariantCulture));
   Set("RangeDetail",s.Overridden ? "meters · manual override" : "meters · calculated");
   Set("Mil",s.Mil.HasValue ? "~"+s.Mil.Value.ToString("0",CultureInfo.InvariantCulture)+" MIL" : "— MIL");
   Set("RangeStatus",s.InRange ? "WITHIN L81 RANGE  /  132–684 M" : s.Range<132 ? "TOO CLOSE  /  L81 MINIMUM 132 M" : "TOO FAR  /  L81 MAXIMUM 684 M");
   Find<TextBlock>("RangeStatus").Foreground=Color(s.InRange ? "#91D6B6" : "#FFBC79");
   Set("Instruction",s.InRange ? String.Format(CultureInfo.InvariantCulture,"Turn to {0:000.0}° {1}. Adjust elevation until the left-side RNG reads {2:0} m. Confirm against your map ping; use the shell camera to check impact.",Math.Round(s.Bearing,1)%360,s.Direction,s.Range)
    : "This distance is outside the community L81 table. Move your in-game mortar or select another target; no elevation estimate is available.");
   Scene.Update(s);Set("SceneState",s.InRange?"Target direction · live from coordinates":"Outside L81 range · elevation unavailable");
  }
  public void SaveTarget() {
   if(Current==null) return;
   Saved.RemoveAll(delegate(SavedTarget t){return t.Origin==Origin.Text && t.Target==Target.Text && t.Distance==Distance.Text;});
   Saved.Insert(0,new SavedTarget{Origin=Origin.Text,Target=Target.Text,Distance=Distance.Text,Summary=String.Format(CultureInfo.InvariantCulture,"{0:HH:mm}   {1:000.0}° {2,-3}   {3,5:0} m   → {4}{5}",DateTime.Now,Math.Round(Current.Bearing,1)%360,Current.Direction,Current.Range,Target.Text,Current.Overridden?"  [override]":"")});
   if(Saved.Count>20)Saved.RemoveAt(20); RefreshHistory(); Persist(); Set("HistoryHint","Saved. Select a row and press Enter to restore its mortar, target, and distance.");
  }
  void RefreshHistory(){History.ItemsSource=null;History.ItemsSource=Saved;Set("HistoryHint",Saved.Count==0?"No saved targets yet. Enter saves your current setup here.":Saved.Count+" saved · most recent first · restores the complete setup");}
  public void Recall(){var t=History.SelectedItem as SavedTarget;if(t==null)return;SetInputs(t.Origin,t.Target,t.Distance);Focus(Target);Set("HistoryHint","Restored mortar, target, and distance from the selected setup.");}
  public void RemoveSelected(){
   var t=History.SelectedItem as SavedTarget;if(t==null)return;
   int index=History.SelectedIndex;bool hadFocus=History.IsKeyboardFocusWithin;
   Saved.Remove(t);RefreshHistory();History.SelectedIndex=Math.Min(index,Saved.Count-1);
   if(hadFocus)History.Focus();Persist();
  }
  void CopyCallout(){if(Current==null)return;try{Clipboard.SetText(Current.Callout);Set("Footer","Copied callout to clipboard.");}catch(System.Runtime.InteropServices.ExternalException){Set("Footer","Clipboard is busy. Try Copy again.");}}
  public bool HandleShortcut(Key key, ModifierKeys modifiers) {
   if(modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&key==Key.C){CopyCallout();return true;}
   if(modifiers==ModifierKeys.Control){
    if(key==Key.D1||key==Key.NumPad1){Focus(Origin);return true;}
    if(key==Key.D2||key==Key.NumPad2){Focus(Target);return true;}
    if(key==Key.D3||key==Key.NumPad3){Focus(Distance);return true;}
    if(key==Key.D4||key==Key.NumPad4){Find<Border>("SceneHost").Focus();return true;}
    if(key==Key.N){NewTarget();return true;}
    if(key==Key.H){History.Focus();if(History.SelectedIndex<0&&Saved.Count>0)History.SelectedIndex=0;return true;}
    if(key==Key.T){Find<CheckBox>("Pin").IsChecked=!Window.Topmost;return true;}
   }
   if(modifiers==ModifierKeys.None&&key==Key.F1){ShowHelp();return true;}
   if(modifiers==ModifierKeys.None&&key==Key.Enter){if(History.IsKeyboardFocusWithin)Recall();else if(Keyboard.FocusedElement is TextBox)SaveTarget();else return false;return true;}
   if(modifiers==ModifierKeys.None&&key==Key.Delete&&History.IsKeyboardFocusWithin){RemoveSelected();return true;}
   return false;
  }
  void OnKey(object sender,KeyEventArgs e){if(HandleShortcut(e.Key,Keyboard.Modifiers))e.Handled=true;}
  public void Persist(){
   if(statePath==null)return;
   try{
    var root=new XElement("WardogsFastCalc",new XElement("Origin",Origin.Text),new XElement("Target",Target.Text),new XElement("Distance",Distance.Text),new XElement("Pinned",Window.Topmost));
    var history=new XElement("History");foreach(var t in Saved)history.Add(new XElement("Setup",new XAttribute("origin",t.Origin),new XAttribute("target",t.Target),new XAttribute("distance",t.Distance),new XAttribute("summary",t.Summary)));root.Add(history);
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(statePath));
    string temp=statePath+".tmp";new XDocument(root).Save(temp);
    if(File.Exists(statePath))File.Replace(temp,statePath,null);else File.Move(temp,statePath);
   }catch(Exception ex){if(!(ex is IOException)&&!(ex is UnauthorizedAccessException))throw;Set("Footer","Could not save this session. Check folder permissions or free disk space.");}
  }
  void LoadState(){
   if(statePath==null||!File.Exists(statePath))return;
   try{
    XElement root;
    var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=131072,MaxCharactersFromEntities=1024 };
    using(var stream=new FileStream(statePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
     if(stream.Length>131072)throw new InvalidDataException("Session exceeds size limit.");
     using(var reader=XmlReader.Create(stream,settings)) root=XDocument.Load(reader).Root;
    }
    if(root==null||root.Name!="WardogsFastCalc")throw new InvalidDataException("Unexpected session document.");
    string origin=(string)root.Element("Origin")??"",target=(string)root.Element("Target")??"",distance=(string)root.Element("Distance")??"";
    if(origin.Length>512||target.Length>512||distance.Length>12)throw new InvalidDataException("Session fields exceed input limits.");
    SetInputs(origin,target,distance);
    Find<CheckBox>("Pin").IsChecked=(string)root.Element("Pinned")=="true";
    var history=root.Element("History");if(history!=null)foreach(var t in history.Elements("Setup").Take(20)) {
     var saved=new SavedTarget{Origin=(string)t.Attribute("origin")??"",Target=(string)t.Attribute("target")??"",Distance=(string)t.Attribute("distance")??"",Summary=(string)t.Attribute("summary")??"Saved target"};
     Coordinate a,b;double range;
     if(saved.Origin.Length>512||saved.Target.Length>512||saved.Distance.Length>12||saved.Summary.Length>1200)continue;
     if(!Calculator.TryCoordinate(saved.Origin,out a)||!Calculator.TryCoordinate(saved.Target,out b))continue;
     if(!String.IsNullOrWhiteSpace(saved.Distance)&&(!Calculator.TryNumber(saved.Distance.Trim(),out range)||range<=0||range>25000))continue;
     Saved.Add(saved);
    }
    RefreshHistory();
   }catch(Exception ex){if(!(ex is IOException)&&!(ex is InvalidDataException)&&!(ex is UnauthorizedAccessException)&&!(ex is XmlException))throw;Saved.Clear();SetInputs("","","");RefreshHistory();Set("Footer","Previous session could not be read. Start with fresh coordinates.");}
  }
  void ShowHelp(){
   var help=new Window{Title="How to use • Mortar Fast Calc",Owner=Window,Width=670,Height=660,MinWidth=480,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Color("#192127"),Foreground=Color("#EDF1F2"),FontFamily=new FontFamily("Segoe UI"),FontSize=14};
   var panel=new StackPanel{Margin=new Thickness(25)};
   panel.Children.Add(new TextBlock{Text="From map to mortar",FontSize=25,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
   string text="1. On the WARDOGS map, right-click your mortar and choose Mark Coordinates. Copy the X/Y pair from the chat input. Paste it into Your mortar. Repeat for your target.\n\n2. Leave Distance blank for automatic range. If the game gives you a measured distance, enter it in meters; the bearing still comes from the coordinates.\n\n3. In the mortar sight, turn to the displayed compass heading and adjust the left-side RNG readout to the displayed meters. Check direction against your target ping. The community MIL estimate is a secondary guide.\n\nKEYBOARD\nTab / Shift+Tab — move between controls\nCtrl+1 / Ctrl+2 / Ctrl+3 — select mortar / target / distance\nEnter in an input — save the current setup\nCtrl+N — clear target and distance, keep your mortar\nCtrl+Shift+C — copy the callout\nCtrl+H — saved targets; arrows then Enter to restore\nDelete in saved targets — remove the selected setup\nCtrl+T — toggle keep on top   ·   F1 — this guide\nAlt+F4 — close the app\n\nCOORDINATES & DATA\nSupported game maps: Bakurani, Ozeti, Zestafona. All three use 100 meters per coordinate unit in the researched community map configuration. X increases east; Y increases north. Compass: 0° N, 90° E, 180° S, 270° W.\n\nAccepted: x98.43, y110.38 · 98.43 110.38 · (98.43, 110.38). Labeled decimal commas also work: x98,43 y110,38. Use decimal points for unlabeled pairs.\n\nThe L81 table covers 132–684 m. MIL values are linearly interpolated from community game measurements, researched 19 September 2026. No terrain-height or obstruction correction is applied. Game patches can change the values; the current in-game HUD is authoritative.\n\nThis is an offline, unofficial game companion. It does not read the game, automate input, or send network traffic. Settings and up to 20 saved setups live in %LOCALAPPDATA%\\WardogsFastCalc\\session.xml.\n\nSOURCE\nApollyon’s open-source wardogs-calculator (MIT), with its data snapshot and license included in this delivery. See RESEARCH.md for exact links and commit.\nhttps://github.com/apollyon-sys/wardogs-calculator";
   text += "\n\n3D ALIGNMENT VIEW\nCtrl+4 focuses the view. Arrow keys orbit; plus/minus zoom; Space toggles overhead; Home resets. You can also drag to orbit and scroll to zoom.\n\nBearing follows the calculated direction. Tube tilt illustrates relative MIL elevation; it is not a literal angle. The target ring is not to scale. Camera controls never change your aiming solution.";
   panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,LineHeight=21});
   var close=new Button{Content="Close · Esc",Padding=new Thickness(18,8,18,8),Margin=new Thickness(0,18,0,0),IsCancel=true};close.Click+=delegate{help.Close();};panel.Children.Add(close);
   help.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};help.ShowDialog();
  }
  public void RenderTo(string path){Window.UpdateLayout();var target=new RenderTargetBitmap((int)Math.Ceiling(Window.ActualWidth),(int)Math.Ceiling(Window.ActualHeight),96,96,PixelFormats.Pbgra32);target.Render(Window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(target));using(var stream=File.Create(path))png.Save(stream);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{
    if(args.Length>0&&args[0]=="--test")return Tests.Run(args.Length>1?args[1]:"test-results.txt");
    var app=new Application();var controller=new Controller(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WardogsFastCalc","session.xml"));app.Run(controller.Window);return 0;
   }catch(Exception ex){MessageBox.Show("Mortar Fast Calc could not start.\n\n"+ex.Message,"Mortar Fast Calc",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
  }
 }
}
