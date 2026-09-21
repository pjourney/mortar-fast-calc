using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WardogsFastCalc {
 public static class DesktopLayout {
  public static Rect Fit(Rect requested,IEnumerable<Rect> workAreas) {
   var areas=workAreas.Where(r=>!r.IsEmpty&&r.Width>0&&r.Height>0).ToList();
   if(areas.Count==0)return requested;
   // Prefer the monitor containing most of the window, then the nearest one.
   Rect area=areas.OrderByDescending(r=>{Rect intersection=Rect.Intersect(r,requested);return intersection.IsEmpty?0:intersection.Width*intersection.Height;})
    .ThenBy(r=>Math.Pow(r.X+r.Width/2-requested.X-requested.Width/2,2)+Math.Pow(r.Y+r.Height/2-requested.Y-requested.Height/2,2)).First();
   double width=Math.Min(area.Width,Math.Max(560,requested.Width)),height=Math.Min(area.Height,Math.Max(500,requested.Height));
   return new Rect(Math.Max(area.Left,Math.Min(area.Right-width,requested.Left)),Math.Max(area.Top,Math.Min(area.Bottom-height,requested.Top)),width,height);
  }
 }
 public sealed partial class Controller {
  Rect? savedBounds;
  bool savedMaximized;
  bool wasMaximized;
  bool arranging;
  public bool IsNarrowLayout { get; private set; }
  void ConfigureDesktop() {
   Window.SourceInitialized+=delegate{RestorePlacement();};
   Window.StateChanged+=delegate{if(Window.WindowState!=WindowState.Minimized)wasMaximized=Window.WindowState==WindowState.Maximized;};
   EventHandler changed=delegate{if(!Window.Dispatcher.HasShutdownStarted)Window.Dispatcher.BeginInvoke(new Action(delegate{if(Window.IsVisible&&Window.WindowState==WindowState.Normal)ApplyBounds(CurrentBounds());}));};
   SystemEvents.DisplaySettingsChanged+=changed;
   Window.Closed+=delegate{SystemEvents.DisplaySettingsChanged-=changed;};
  }
  public void UpdateLayout() {
   if(arranging)return;arranging=true;
   try {
    var scroll=Find<ScrollViewer>("BodyScroll");var grid=Find<Grid>("BodyGrid");
    IsNarrowLayout=Window.ActualWidth<1000;
    Find<Grid>("RootLayout").Margin=new Thickness(IsNarrowLayout?14:22,16,IsNarrowLayout?14:22,12);
    Find<TextBlock>("AppTitle").FontSize=IsNarrowLayout?24:28;
    var input=Find<Border>("InputCard");var scene=Find<Border>("SceneCard");var history=Find<Border>("HistoryCard");
    grid.ColumnDefinitions[0].Width=IsNarrowLayout?new GridLength(1,GridUnitType.Star):new GridLength(340);
    grid.ColumnDefinitions[1].Width=new GridLength(IsNarrowLayout?0:16);
    grid.ColumnDefinitions[2].Width=IsNarrowLayout?new GridLength(0):new GridLength(1,GridUnitType.Star);
    Grid.SetColumn(scene,IsNarrowLayout?0:2);Grid.SetColumn(history,IsNarrowLayout?0:2);
    Grid.SetRowSpan(input,IsNarrowLayout?1:2);Grid.SetRow(scene,IsNarrowLayout?1:0);Grid.SetRow(history,IsNarrowLayout?2:1);
    input.Margin=new Thickness(0,0,0,IsNarrowLayout?14:0);
    scene.Height=IsNarrowLayout?310:Double.NaN;
    history.Height=IsNarrowLayout?340:Double.NaN;
    grid.RowDefinitions[0].Height=IsNarrowLayout?GridLength.Auto:new GridLength(1,GridUnitType.Star);
    grid.RowDefinitions[1].Height=IsNarrowLayout?GridLength.Auto:new GridLength(Math.Max(290,Math.Min(370,scroll.ActualHeight*.46)));
    grid.RowDefinitions[2].Height=IsNarrowLayout?GridLength.Auto:new GridLength(0);
    // A short window scrolls only the controls; aiming numbers stay fixed above it.
    if(IsNarrowLayout)grid.Height=Double.NaN;
    else {input.Measure(new Size(340,Double.PositiveInfinity));grid.Height=Math.Max(input.DesiredSize.Height,Math.Max(540,scroll.ActualHeight));}
   } finally {arranging=false;}
  }
  Rect[] WorkAreas() {
   var source=PresentationSource.FromVisual(Window);Matrix fromPixels=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice:Matrix.Identity;
   return System.Windows.Forms.Screen.AllScreens.OrderByDescending(s=>s.Primary).Select(s=> {
    var a=s.WorkingArea;return Rect.Transform(new Rect(a.X,a.Y,a.Width,a.Height),fromPixels);
   }).ToArray();
  }
  Rect CurrentBounds(){return Window.WindowState==WindowState.Normal?new Rect(Window.Left,Window.Top,Window.ActualWidth,Window.ActualHeight):Window.RestoreBounds;}
  void ApplyBounds(Rect bounds) {
   Rect fit=DesktopLayout.Fit(bounds,WorkAreas());
   Window.MinWidth=Math.Min(560,fit.Width);Window.MinHeight=Math.Min(500,fit.Height);
   Window.Left=fit.Left;Window.Top=fit.Top;Window.Width=fit.Width;Window.Height=fit.Height;
  }
  void RestorePlacement() {
   var areas=WorkAreas();Rect primary=areas.Length>0?areas[0]:SystemParameters.WorkArea;
   Rect initial=savedBounds??new Rect(primary.Left+(primary.Width-Window.Width)/2,primary.Top+(primary.Height-Window.Height)/2,Window.Width,Window.Height);
   Window.WindowStartupLocation=WindowStartupLocation.Manual;ApplyBounds(initial);
   if(savedMaximized)Window.WindowState=WindowState.Maximized;
  }
  void ReadPlacement(XElement root) {
   var placement=root.Element("Window");if(placement==null)return;
   double x,y,w,h;
   if(!Calculator.TryNumber((string)placement.Attribute("left"),out x)||!Calculator.TryNumber((string)placement.Attribute("top"),out y)||
      !Calculator.TryNumber((string)placement.Attribute("width"),out w)||!Calculator.TryNumber((string)placement.Attribute("height"),out h)||
      Math.Abs(x)>1000000||Math.Abs(y)>1000000||w<1||h<1||w>100000||h>100000)return;
   savedBounds=new Rect(x,y,w,h);savedMaximized=(string)placement.Attribute("maximized")=="true";
  }
  void WritePlacement(XElement root) {
   Rect bounds=Window.IsLoaded?CurrentBounds():savedBounds??Rect.Empty;
   if(bounds.IsEmpty||Double.IsNaN(bounds.X)||Double.IsNaN(bounds.Y)||bounds.Width<=0||bounds.Height<=0)return;
   root.Add(new XElement("Window",new XAttribute("left",bounds.X.ToString("0.###",CultureInfo.InvariantCulture)),new XAttribute("top",bounds.Y.ToString("0.###",CultureInfo.InvariantCulture)),
    new XAttribute("width",bounds.Width.ToString("0.###",CultureInfo.InvariantCulture)),new XAttribute("height",bounds.Height.ToString("0.###",CultureInfo.InvariantCulture)),new XAttribute("maximized",Window.IsLoaded?Window.WindowState==WindowState.Maximized||Window.WindowState==WindowState.Minimized&&wasMaximized:savedMaximized)));
  }
 }
}
