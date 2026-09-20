using FoxCursor.Core;

int passed = 0;
var tests = new (string Name, Action Run)[]
{
    ("点击优先于键盘", () => { var a=New(); a.Keyboard(1); a.Pointer(PointerAction.LeftDown,1,0,0); Equal(FoxState.LeftClick,a.Step(1.1,new()).State); }),
    ("拖拽优先于点击", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); a.BeginDrag(1.01); Equal(FoxState.Dragging,a.Step(1.1,new()).State); }),
    ("拖拽释放后弹跳并归位", () => { var a=New(); a.BeginDrag(1); a.Pointer(PointerAction.LeftUp,2,0,0); Equal(FoxState.DoubleClick,a.Step(2.1,new()).State); Equal(FoxState.Idle,a.Step(3,new()).State); }),
    ("Escape 取消长按和拖拽", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); a.BeginDrag(1.1); a.Cancel(2); Equal(FoxState.Idle,a.Step(2.1,new()).State); Check(!a.Held); }),
    ("长按进入蓄力", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); Equal(FoxState.Charging,a.Step(1.6,new()).State); a.Pointer(PointerAction.LeftUp,2,0,0); Equal(FoxState.Idle,a.Step(2,new()).State); }),
    ("系统双击时间与空间约束", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,100,100,.5,4,4); a.Pointer(PointerAction.LeftUp,1.1,100,100); a.Pointer(PointerAction.LeftDown,1.2,101,100,.5,4,4); Equal(FoxState.DoubleClick,a.Step(1.21,new()).State); }),
    ("远处点击不误识别为双击", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); a.Pointer(PointerAction.LeftDown,1.2,20,0); Equal(FoxState.LeftClick,a.Step(1.21,new()).State); }),
    ("过慢点击不误识别为双击", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); a.Pointer(PointerAction.LeftDown,1.8,0,0); Equal(FoxState.LeftClick,a.Step(1.81,new()).State); }),
    ("三连击第三次重新计数", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); a.Pointer(PointerAction.LeftDown,1.1,0,0); a.Pointer(PointerAction.LeftDown,1.2,0,0); Equal(FoxState.LeftClick,a.Step(1.21,new()).State); }),
    ("键盘在 650ms 后恢复", () => { var a=New(); a.Keyboard(1); Equal(FoxState.KeyboardTyping,a.Step(1.5,new()).State); Equal(FoxState.Idle,a.Step(1.7,new()).State); }),
    ("禁用键盘动画不进入键盘状态", () => { var a=New(); a.Keyboard(1); Equal(FoxState.Idle,a.Step(1.1,new() { KeyboardAnimation=false }).State); }),
    ("禁用拖拽反馈保持低优先级状态", () => { var a=New(); a.BeginDrag(1); Equal(FoxState.Idle,a.Step(1.1,new() { DragAnimation=false }).State); }),
    ("高速运动与方向相关的尾巴", () => { var a=New(); var b=New(); for(int i=0;i<15;i++) { a.Move(i*40,i*20,1+i/60d); b.Move(i*40,-i*20,1+i/60d); a.Step(1+i/60d,new()); b.Step(1+i/60d,new()); } Check(a.Speed>1100); Check(a.Step(1.25,new()).TailAngle < b.Step(1.25,new()).TailAngle); }),
    ("停止后弹簧收敛且停止渲染", () => { var a=New(); for(int i=0;i<15;i++){a.Move(i*40,i*20,1+i/60d);a.Step(1+i/60d,new());} Pose pose=default; for(int i=0;i<200;i++) pose=a.Step(1.3+i/60d,new()); Check(Math.Abs(pose.TailAngle)<.1); Check(!a.NeedsFrames(5,new())); }),
    ("延迟帧不会产生 NaN 或无限振荡", () => { var a=New(); a.Move(0,0,1); a.Move(999999,-99999,1.001); for(int i=0;i<50;i++) {var p=a.Step(2+i*1.7,new()); Check(double.IsFinite(p.TailAngle)); Check(Math.Abs(p.TailAngle)<45);} }),
    ("滚轮影响叶子但最终回弹", () => { var a=New(); a.Pointer(PointerAction.Wheel,1,0,0,wheel:120); var p=a.Step(1.02,new()); Check(p.TailAngle>0); for(int i=0;i<240;i++) p=a.Step(1.03+i/60d,new()); Check(Math.Abs(p.TailAngle)<.1); }),
    ("动画总开关令姿态静止", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); var p=a.Step(1.1,new() { Animations=false }); Equal(1d,p.ScaleX); Equal(0d,p.TailAngle); Check(!a.NeedsFrames(1.1,new(){Animations=false})); }),
    ("零强度保留原始比例", () => { var a=New(); a.Pointer(PointerAction.LeftDown,1,0,0); var p=a.Step(1.1,new(){Intensity=0}); Equal(1d,p.ScaleX); Equal(1d,p.ScaleY); Equal(0d,p.Bounce); }),
    ("待机仅短暂刷新并可被输入打断", () => {var a=New(); Check(a.NeedsFrames(8.5,new())); Check(!a.NeedsFrames(12,new())); a.Keyboard(12); Equal(FoxState.KeyboardTyping,a.Step(12.1,new()).State);}),
    ("Reset 清除旧输入状态", () => {var a=New(); a.BeginDrag(1); a.Keyboard(1); a.Reset(2); Equal(FoxState.Idle,a.Step(2,new()).State); Check(!a.NeedsFrames(2,new()));}),
    ("设置边界与非有限值修正", () => {var p=new Preferences { Size=double.NaN, Opacity=-1, OffsetX=999, HotspotY=2, Intensity=double.PositiveInfinity };p.Normalize();Equal(80d,p.Size);Equal(.2,p.Opacity);Equal(180d,p.OffsetX);Equal(1d,p.HotspotY);Equal(.65,p.Intensity);}),
    ("设置原子保存与损坏回退", () => {string dir=Path.Combine(Path.GetTempPath(),"FoxCursorTests-"+Guid.NewGuid());string path=Path.Combine(dir,"settings.json");try {PreferenceFile.Save(path,new(){Size=99,KeyboardAnimation=false});var p=PreferenceFile.Load(path);Equal(99d,p.Size);Check(!p.KeyboardAnimation);Check(!File.Exists(path+".tmp"));File.WriteAllText(path,"{ broken");Equal(80d,PreferenceFile.Load(path).Size);}finally{Directory.Delete(dir,true);}})
};
foreach(var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); passed++; }
    catch(Exception ex) { Console.Error.WriteLine("FAIL " + test.Name + ": " + ex.Message); }
}
Console.WriteLine($"{passed}/{tests.Length} scenarios passed.");
return passed == tests.Length ? 0 : 1;
static AnimationController New() {var a=new AnimationController();a.Reset(0);return a;}
static void Check(bool condition) {if(!condition)throw new Exception("Condition was false.");}
static void Equal<T>(T expected,T actual) {if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}.");}
