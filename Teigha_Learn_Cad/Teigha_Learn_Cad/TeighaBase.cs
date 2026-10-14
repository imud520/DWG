using ComponentFactory.Krypton.Toolkit;
using ComponentFactory.Krypton.Docking;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.GraphicsSystem;
using Teigha.Runtime;
using TeighaCad_Bll;
using Teigha.Export_Import;
using System.IO;
using Teigha.GraphicsInterface;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Polyline = Teigha.DatabaseServices.Polyline;
using Point2d = Teigha.Geometry.Point2d;
using Point3d = Teigha.Geometry.Point3d;
using Point = System.Drawing.Point;
using OpenCvSharp;
using System.Diagnostics;













namespace Teigha_Learn_Cad
{
    public partial class TeighaBase : KryptonForm
    {
        public static List<Portsc> Wbjc = new List<Portsc>();
        Portsc popppo = new Portsc();
         double nm1 = 100;
        string sq;
        // 定义颜色列表
        List<Teigha.Colors.Color> colors = new List<Teigha.Colors.Color>
        {
             Teigha.Colors.Color.FromRgb(255, 0, 0),    // 红色
             Teigha.Colors.Color.FromRgb(255, 165, 0),  // 橘黄色
             Teigha.Colors.Color.FromRgb(0, 0, 255),    // 蓝色
             Teigha.Colors.Color.FromRgb(0, 255, 0),    // 绿色
             Teigha.Colors.Color.FromRgb(255, 255, 0),  // 黄色
             Teigha.Colors.Color.FromRgb(128, 0, 128),  // 紫色
             Teigha.Colors.Color.FromRgb(0, 255, 255),  // 青色
             Teigha.Colors.Color.FromRgb(255, 192, 203),// 粉色
             Teigha.Colors.Color.FromRgb(128, 128, 128),// 灰色
             Teigha.Colors.Color.FromRgb(255, 255, 255) // 白色
        };
        // 定义颜色索引计数器
        int colorIndex = 0;
        #region 防止闪烁
        [DllImport("dwmapi.dll", EntryPoint = "DwmEnableComposition")]
        protected extern static uint Win32DwmEnableComposition(uint uCompositionAction);

        /// <summary>
        /// 防止闪烁
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }
        #endregion

        #region 基本变量
        Teigha.Runtime.Services dd;
        public Graphics graphics;
        public Teigha.GraphicsSystem.LayoutHelperDevice helperDevice;
        public Database database = null;
        ObjectIdCollection selected = new ObjectIdCollection();
        public Point MouseDownPt = new Point();
        public Point MovePoint = new Point();
        public LayoutManager lm;
        public Point3d firstCornerPoint;
        public int bZoomWindow = -1;
        public string DwgPath = "";

        //--添加
        Color colorAll = Color.Red;
        #endregion

        #region 动作
        public Operations OperModel;
        public List<Entity> selectedClone = new List<Entity>();//临时存放实体
        RectFram selRect = null;
        public InvokerCommad Invoker;
        #endregion

        public TeighaBase()
        {
            InitializeComponent();

            dd = PublicClass.ServiceD;
            SystemObjects.DynamicLinker.LoadApp("GripPoints", false, false);
            SystemObjects.DynamicLinker.LoadApp("PlotSettingsValidator", false, false);

            this.DoubleBuffered = true;
            HostApplicationServices.Current = new HostAppServ(dd);
            Environment.SetEnvironmentVariable("DDPLOTSTYLEPATHS", ((HostAppServ)HostApplicationServices.Current).FindConfigPath(String.Format("PrinterStyleSheetDir")));
            this.basepanel.Focus();
            this.basepanel.MouseWheel += Basepanel_MouseWheel;
            this.basepanel.Paint += Basepanel_Paint;
            this.basepanel.Resize += Basepanel_Resize;
            this.basepanel.MouseClick += Basepanel_MouseClick;
            this.basepanel.MouseDown += Basepanel_MouseDown;
            this.basepanel.MouseMove += Basepanel_MouseMove;
            this.basepanel.MouseUp += Basepanel_MouseUp;
            this.Paint += Basepanel_Paint;

            Invoker = new InvokerCommad();
            Invoker.ChangeStack += Invoker_ChangeStack;

        }

        private void TeighaBase_Load(object sender, EventArgs e)
        {
            //file_open_handlerNotBar(@"C:\Users\admin\Desktop\SVN\Interesting\Teigha_Learn_Cad_Bin\OdWriteEx XRef.dwg");
        }

        #region 基本事件
        private void Basepanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!DragSelected)
            {
                using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                {
                    // camera position in world coordinates 摄像机在世界坐标中的位置
                    Point3d pos = pView.Position;
                    // TransformBy() returns a transformed copy TransformBy()返回转换后的副本
                    pos = pos.TransformBy(pView.WorldToDeviceMatrix);
                    int vx = (int)pos.X;
                    int vy = (int)pos.Y;
                    vx = e.X - vx;
                    vy = e.Y - vy;
                    // we move point of view to the mouse location, to create an illusion of scrolling in/out there
                    //我们将视图移动到鼠标位置，以创建一个在那里滚动的错觉
                    dolly(pView, -vx, -vy);
                    //注意，我们实际上忽略了delta值(符号足以说明问题)
                    // note that we essentially ignore delta value (sign is enough for illustrative purposes)
                    double num = e.Delta > 0 ? 1.0 / 0.9 : 0.9;
                    pView.Zoom(num);
                    nm1 *= num;
                    toolStripLabel1.Text = (int)nm1 + "%";
                    dolly(pView, vx, vy);
                    ReceiverViews receive = new ReceiverViews(num, pos, MovePoint, ref helperDevice);
                    Invoker.SetCommad(receive);
                    Invalidate();
                }
            }
        }

        private readonly object _updateLock = new object();

        public void Basepanel_Paint(object sender, PaintEventArgs e)
        {
            if (helperDevice != null)
            {
                try
                {
                    if (helperDevice == null || helperDevice.IsDisposed)
                    {
                        MessageBox.Show("helperDevice 未初始化或已被释放。");
                        return;
                    }

                    helperDevice.Update();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    return;
                }
            }
            /*            lock (_updateLock)
                        {
                            if (helperDevice != null)
                            {
                                try
                                {
                                    helperDevice.Update();
                                }
                                catch (System.Exception ex)
                                {
                                    MessageBox.Show("Update 调用错误: " + ex.ToString());
                                }
                            }
                        }*/

            switch (OperModel)
            {
                case Operations.None:
                    break;
                case Operations.Drag:
                    break;
                case Operations.Roll:
                    break;
                case Operations.SelectFrom:
                    break;
                case Operations.MoveEntity:
                    break;
                case Operations.Gyroscope:
                    break;
                case Operations.RollEntity:
                    break;
                case Operations.InsertEntity:
                    switch (EntType)
                    {
                        case EntityType.Line:
                            break;
                        case EntityType.Polyline:
                            break;
                        case EntityType.None:
                            break;
                        case EntityType.Arc:
                            if (DragSelected)
                                DrawArcs();
                            break;
                        case EntityType.Circle:
                            if (DragSelected)
                                DrawCircle();
                            break;
                        default:
                            break;
                    }
                    break;
                case Operations.PanelDrawPen:
                    DrawPentrack();
                    break;
                default:
                    break;
            }

            // 仅在标志变量为 true 时绘制红色矩形框
            if (Global.drawRectangle)
            {
                int x = Math.Min(Global.screenX1, Global.screenX2);
                int y = Math.Min(Global.screenY1, Global.screenY2);
                int width = Math.Abs(Global.screenX2 - Global.screenX1);
                int height = Math.Abs(Global.screenY2 - Global.screenY1);

                DrawRedRectangle(e.Graphics, x, y, width, height);
            }
        }

        private void Basepanel_Resize(object sender, EventArgs e)
        {
            resize();
        }

        private void Basepanel_MouseClick2(object sender, MouseEventArgs e)
        {
            if (bZoomWindow > -1 && bZoomWindow < 2)
            {
                if (bZoomWindow == 1)
                {
                    bZoomWindow = -1;
                    ZoomWindow(firstCornerPoint, toEyeToWorld(e.X, e.Y));
                }
                if (bZoomWindow == 0)
                {
                    firstCornerPoint = toEyeToWorld(e.X, e.Y);
                    bZoomWindow = 1;
                }
            }
            
            if (Global.drawRectangle == false)
            {
                Global.screenX1 = e.X;
                Global.screenY1 = e.Y;
                Global.x1 = toEyeToWorld(e.X, e.Y).X;
                Global.y1 = toEyeToWorld(e.X, e.Y).Y;
                Global.drawRectangle = true; // 更改为true以指示已点击第一个点

                // 提示用户点击第二个点
                MessageBox.Show("请点击第二个点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Global.screenX2 = e.X;
                Global.screenY2 = e.Y;
                Global.x2 = toEyeToWorld(e.X, e.Y).X;
                Global.y2 = toEyeToWorld(e.X, e.Y).Y;
                Global.mlist.Add(Global.x1);
                Global.mlist.Add(Global.y1);
                Global.mlist.Add(Global.x2);
                Global.mlist.Add(Global.y2);
                Global.drawRectangle = true; // 改为false以指示完成了矩形绘制
                basepanel.Invalidate();

                if (Global.mlist.Count == 4)
                {
                    this.basepanel.MouseClick -= Basepanel_MouseClick2;
                    MessageBox.Show("请选择线束路径中任一电线", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    Global.drawRectangle = false; // 清除矩形框
                    basepanel.Invalidate(); // 重新绘制面板以清除矩形框
                    this.basepanel.MouseDown += Basepanel_MouseDown2;
                }
            }

            if (!basepanel.Focused)
                basepanel.Focus();
        }

        private void DrawRedRectangle(Graphics g, int x, int y, int width, int height)
        {
            using (Pen redPen = new Pen(Color.Red, 2)) // 使用红色画笔，宽度为2
            {
                g.DrawRectangle(redPen, x, y, width, height);
            }
        }


        public static class GlobalData
        {
            public static Entity globalent { get; set; }

        }
        public static class GlobalLine
        {
            public static Line globalline { get; set; }

        }
        private string ShowCustomDialog()
        {
            using (CustomDialogForm dialogForm = new CustomDialogForm())
            {
                if (dialogForm.ShowDialog() == DialogResult.OK)
                {
                    return dialogForm.SelectedOption;
                }
            }

            return string.Empty;
        }
        public static class gselect
        {
            public static string select;
        }
        public class GlobalEntityData
        {
            public static ObjectId SelectedEntityId { get; set; }
            public static Entity SelectedEntity { get; set; }
        }
        private void Basepanel_MouseClick_ele(object sender, MouseEventArgs e)
        {
            if (bZoomWindow > -1 && bZoomWindow < 2)
            {
                if (bZoomWindow == 1)
                {
                    bZoomWindow = -1;
                    ZoomWindow(firstCornerPoint, toEyeToWorld(e.X, e.Y));
                }
                if (bZoomWindow == 0)
                {
                    firstCornerPoint = toEyeToWorld(e.X, e.Y);
                    bZoomWindow = 1;
                }
            }

            if (Global.drawRectangle == false)
            {
                Global.screenX1 = e.X;
                Global.screenY1 = e.Y;
                Global.x1 = toEyeToWorld(e.X, e.Y).X;
                Global.y1 = toEyeToWorld(e.X, e.Y).Y;
                Global.drawRectangle = true; // 更改为true以指示已点击第一个点

                // 提示用户点击第二个点
                MessageBox.Show("请点击第二个点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Global.screenX2 = e.X;
                Global.screenY2 = e.Y;
                Global.x2 = toEyeToWorld(e.X, e.Y).X;
                Global.y2 = toEyeToWorld(e.X, e.Y).Y;
                Global.mlist.Add(Global.x1);
                Global.mlist.Add(Global.y1);
                Global.mlist.Add(Global.x2);
                Global.mlist.Add(Global.y2);
                Global.drawRectangle = true; // 改为false以指示完成了矩形绘制
                basepanel.Invalidate();

                if (Global.mlist.Count == 4)
                {
                    this.basepanel.MouseClick -= Basepanel_MouseClick_ele;
                    MessageBox.Show("请选择一条电源主线。", "提示");
                    Global.drawRectangle = false; // 清除矩形框
                    basepanel.Invalidate(); // 重新绘制面板以清除矩形框
                    this.basepanel.MouseDown += Basepanel_MouseDown2;
                }
            }

            if (!basepanel.Focused)
                basepanel.Focus();
        }
        private void Basepanel_MouseDown2(object sender, MouseEventArgs e)
        {
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNEw = new ObjectIdCollection();
                pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                  new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectNEw != null && selectNEw.Count > 0)
                {
                    selected = selectNEw;
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                            {
                                ent.Highlight();
                                GlobalEntityData.SelectedEntityId = ent.ObjectId;
                                GlobalEntityData.SelectedEntity = ent;

                                if (GlobalEntityData.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobalEntityData.SelectedEntity;
                                    string entityType = ent1.GetType().Name;
                                    if (ent1 is Line line)
                                    {
                                        MessageBox.Show(entityType, "类型");

                                        // 判断线段方向
                                        if (line.StartPoint.X < line.EndPoint.X)
                                        {
                                            // 如果线段方向满足条件，直接赋值
                                            Global.fline = new Line(line.StartPoint, line.EndPoint);
                                        }
                                        else
                                        {
                                            // 如果线段方向不满足条件，反转线段方向
                                            Global.fline = new Line(line.EndPoint, line.StartPoint);
                                        }
                                    }
                                    else if (ent1 is Teigha.DatabaseServices.Polyline pline)
                                    {
                                        MessageBox.Show(entityType, "类型");
                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请重新使用此功能", "提示");
                                    }

                                    this.basepanel.MouseDown -= Basepanel_MouseDown2;
                                    if (gselect.select == "线束图")
                                    {
                                        MessageBox.Show("请选择线长文本（应为数字）", "线束图", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    }
                                    else if (gselect.select == "电器图")
                                    {
                                        MessageBox.Show("请选择引脚文本", "电器图", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    }
                                    //MessageBox.Show("请选择一个文本", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    this.basepanel.MouseDown += Basepanel_MouseDown3;
                                    break;
                                }

                            }
                        }
                    }

                }
            }
        }

        public class GlobaltextData1
        {
            public static ObjectId SelectedEntityId { get; set; }
            public static Entity SelectedEntity { get; set; }
        }
        private void Basepanel_MouseDown3(object sender, MouseEventArgs e)
        {
            //MessageBox.Show("请选择一条直线", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            //这里是获取点击的点，电源主线
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNEw = new ObjectIdCollection();
                pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                  new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectNEw != null && selectNEw.Count > 0)
                {
                    selected = selectNEw;
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                            {
                                ent.Highlight();
                                GlobaltextData1.SelectedEntityId = ent.ObjectId;
                                GlobaltextData1.SelectedEntity = ent;

                                if (GlobaltextData1.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobaltextData1.SelectedEntity;
                                    string entityType = ent1.GetType().Name;
                                    if (ent1 is DBText textEntity)
                                    {
                                        string textContent = textEntity.TextString;
                                        MessageBox.Show(textContent.ToString(), "内容");
                                        Global.dk_height = textEntity.Height;
                                    }
                                    else if (ent1 is MText textEntity2)
                                    {
                                        string textContent2 = textEntity2.Text;
                                        MessageBox.Show(textContent2.ToString(), "内容");
                                        Global.dk_height = textEntity2.TextHeight;
                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请重新使用此功能", "提示");
                                    }

                                    this.basepanel.MouseDown -= Basepanel_MouseDown3;
                                    if (gselect.select == "线束图")
                                    {
                                        MessageBox.Show("请选择接插件名称", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    }
                                    else if (gselect.select == "电器图")
                                    {
                                        MessageBox.Show("请选择负载名称", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                    }
                                    this.basepanel.MouseDown += Basepanel_MouseDown4;
                                    break;
                                }

                            }
                        }
                    }

                }
            }
        }

        public class GlobaltextData2
        {
            public static ObjectId SelectedEntityId { get; set; }
            public static Entity SelectedEntity { get; set; }
        }
        private void Basepanel_MouseDown4(object sender, MouseEventArgs e)
        {
            //MessageBox.Show("请选择一条直线", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNEw = new ObjectIdCollection();
                pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                  new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectNEw != null && selectNEw.Count > 0)
                {
                    selected = selectNEw;
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                            {
                                ent.Highlight();
                                GlobaltextData2.SelectedEntityId = ent.ObjectId;
                                GlobaltextData2.SelectedEntity = ent;

                                if (GlobaltextData2.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobaltextData2.SelectedEntity;
                                    string entityType = ent1.GetType().Name;
                                    if (ent1 is DBText textEntity)
                                    {
                                        string textContent = textEntity.TextString;
                                        MessageBox.Show(textContent.ToString(), "内容");
                                        Global.qj_height = textEntity.Height;
                                    }
                                    else if (ent1 is MText textEntity2)
                                    {
                                        string textContent2 = textEntity2.Text;
                                        MessageBox.Show(textContent2.ToString(), "内容");
                                        Global.qj_height = textEntity2.TextHeight;

                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请重新使用此功能", "提示");
                                    }

                                    if (gselect.select == "电器图")
                                    {
                                        MessageBox.Show("请选择继电器名称", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                        this.basepanel.MouseDown -= Basepanel_MouseDown4;
                                        this.basepanel.MouseDown += Basepanel_MouseDown5;
                                    }
                                    else
                                    {
                                        this.basepanel.MouseDown -= Basepanel_MouseDown4;
                                    }
                                    break;
                                }

                            }
                        }
                    }

                }
            }
        }

        public class GlobaltextData3
        {
            public static ObjectId SelectedEntityId { get; set; }
            public static Entity SelectedEntity { get; set; }
        }
        //private void Basepanel_MouseDown5(object sender, MouseEventArgs e)
        //{
        //    MouseDownPt = new Point(e.X, e.Y);
        //    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
        //    {
        //        ObjectIdCollection selectNEw = new ObjectIdCollection();
        //        pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
        //          new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

        //        if (selectNEw != null && selectNEw.Count > 0)
        //        {
        //            selected = selectNEw;
        //            foreach (ObjectId item in selected)
        //            {
        //                if (!item.IsNull && !item.IsErased)
        //                {
        //                    using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead))
        //                    {
        //                        ent.Highlight();
        //                        GlobaltextData2.SelectedEntityId = ent.ObjectId;
        //                        GlobaltextData2.SelectedEntity = ent;

        //                        if (GlobaltextData2.SelectedEntity != null)
        //                        {
        //                            Entity ent1 = GlobaltextData2.SelectedEntity;
        //                            string entityType = ent1.GetType().Name;
        //                            if (ent1 is DBText textEntity)
        //                            {
        //                                string textContent = textEntity.TextString;
        //                                MessageBox.Show(textContent.ToString(), "内容");
        //                                Global.jd_height = textEntity.Height;

        //                            }
        //                            else if (ent1 is MText textEntity2)
        //                            {
        //                                string textContent2 = textEntity2.Text;
        //                                MessageBox.Show(textContent2.ToString(), "内容");
        //                                Global.jd_height = textEntity2.TextHeight;

        //                            }
        //                            else
        //                            {
        //                                MessageBox.Show("选择有误，请重新使用此功能", "提示");
        //                            }

        //                            if (gselect.select == "电器图")
        //                            {
        //                                MessageBox.Show("请选择保险丝文本", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        //                                this.basepanel.MouseDown -= Basepanel_MouseDown5;
        //                                this.basepanel.MouseDown += Basepanel_MouseDown6;
        //                            }
        //                            else
        //                            {
        //                                this.basepanel.MouseDown -= Basepanel_MouseDown5;
        //                            }
        //                            break;
        //                        }

        //                    }
        //                }
        //            }

        //        }
        //    }
        //}

        private void Basepanel_MouseDown5(object sender, MouseEventArgs e)
        {
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNEw = new ObjectIdCollection();
                pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                  new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectNEw != null && selectNEw.Count > 0)
                {
                    selected = selectNEw;
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                            {
                                ent.Highlight();
                                GlobaltextData2.SelectedEntityId = ent.ObjectId;
                                GlobaltextData2.SelectedEntity = ent;

                                if (GlobaltextData2.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobaltextData2.SelectedEntity;
                                    string entityType = ent1.GetType().Name;
                                    if (ent1 is DBText textEntity)
                                    {
                                        string textContent = textEntity.TextString;
                                        MessageBox.Show(textContent.ToString(), "内容");
                                        Global.jd_height = textEntity.Height;

                                        // 新增竖线查找逻辑 
                                        FindNearestVerticalLines(textEntity.Position, database); // 传入文本位置和数据库对象 
                                    }
                                    else if (ent1 is MText textEntity2)
                                    {
                                        string textContent2 = textEntity2.Text;
                                        MessageBox.Show(textContent2.ToString(), "内容");
                                        Global.jd_height = textEntity2.TextHeight;

                                        // 新增竖线查找逻辑 
                                        FindNearestVerticalLines(textEntity2.Location, database);
                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请重新使用此功能", "提示");
                                    }

                                    if (gselect.select == "电器图")
                                    {
                                        MessageBox.Show("请选择保险丝文本", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                        this.basepanel.MouseDown -= Basepanel_MouseDown5;
                                        this.basepanel.MouseDown += Basepanel_MouseDown6;
                                    }
                                    else
                                    {
                                        this.basepanel.MouseDown -= Basepanel_MouseDown5;
                                    }
                                    break;
                                }

                            }
                        }
                    }

                }
            }
        }

        private void FindNearestVerticalLines(Point3d textPosition, Database database)
        {
            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                BlockTableRecord currentSpace = tr.GetObject(database.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;

                double searchRadius = 40; // 搜索半径

                // 1. 查找所有闭合4边矩形多段线，且最近距离在searchRadius内
                List<Polyline> rectanglePolylines = new List<Polyline>();

                foreach (ObjectId id in currentSpace)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pl)
                    {
                        if (IsRectanglePolyline(pl)) // 已限定闭合4边矩形
                        {
                            double dist = GetDistancePointToPolyline(pl, textPosition);
                            if (dist <= searchRadius)
                            {
                                rectanglePolylines.Add(pl);
                            }
                        }
                    }
                }

                if (rectanglePolylines.Count > 0)
                {
                    Polyline nearestRectangle = null;
                    double minDist = double.MaxValue;

                    foreach (var pl in rectanglePolylines)
                    {
                        double dist = GetDistancePointToPolyline(pl, textPosition);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            nearestRectangle = pl;
                        }
                    }

                    if (nearestRectangle != null)
                    {
                        GetRectangleLengthWidth(nearestRectangle, out double length, out double width);

                        Global.jdqrectangleLength = length;
                        Global.jdqrectangleWidth = width;

                        tr.Commit();
                        return; // 找到矩形直接返回
                    }
                }

                // 2. 找不到矩形多段线，继续找竖直线段

                List<Line> verticalLines = new List<Line>();

                foreach (ObjectId id in currentSpace)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is Line line &&
                        Math.Abs(line.StartPoint.X - line.EndPoint.X) < 0.01 &&
                        line.Length > 1)
                    {
                        Point3d midPoint = new Point3d(
                            (line.StartPoint.X + line.EndPoint.X) / 2,
                            (line.StartPoint.Y + line.EndPoint.Y) / 2,
                            (line.StartPoint.Z + line.EndPoint.Z) / 2
                        );
                        double distance = textPosition.DistanceTo(midPoint);

                        if (distance <= searchRadius)
                        {
                            verticalLines.Add(line);
                        }
                    }
                }

                if (verticalLines.Count >= 2)
                {
                    var orderedLines = verticalLines
                        .OrderBy(l => textPosition.DistanceTo(
                            new Point3d(
                                (l.StartPoint.X + l.EndPoint.X) / 2,
                                (l.StartPoint.Y + l.EndPoint.Y) / 2,
                                (l.StartPoint.Z + l.EndPoint.Z) / 2
                            )
                        ))
                        .ToList();

                    Line firstLine = null;
                    Line secondLine = null;

                    for (int i = 0; i < orderedLines.Count - 1; i++)
                    {
                        firstLine = orderedLines[i];

                        secondLine = orderedLines.Skip(i + 1)
                            .FirstOrDefault(l =>
                                Math.Abs(l.Length - firstLine.Length) < 0.01 &&
                                Math.Abs(l.StartPoint.X - firstLine.StartPoint.X) > 1
                            );

                        if (secondLine != null)
                        {
                            break;
                        }
                    }

                    if (secondLine != null)
                    {
                        Global.jdqrectangleLength = firstLine.Length;
                        Global.jdqrectangleWidth = Math.Abs(firstLine.StartPoint.X - secondLine.StartPoint.X);
                    }
                    else
                    {
                        MessageBox.Show("未找到匹配长度的第二条竖线", "警告");
                    }
                }
                else
                {
                    MessageBox.Show("附近未找到足够垂直线段", "警告");
                }

                tr.Commit();
            }
        }

        // 判断是否闭合4边矩形多段线
        private bool IsRectanglePolyline(Polyline pl)
        {
            if (pl == null) return false;

            if (!pl.Closed) return false;

            if (pl.NumberOfVertices != 4) return false;

            for (int i = 0; i < 4; i++)
            {
                Point2d prev = pl.GetPoint2dAt((i + 3) % 4);
                Point2d curr = pl.GetPoint2dAt(i);
                Point2d next = pl.GetPoint2dAt((i + 1) % 4);

                Vector2d v1 = curr - prev;
                Vector2d v2 = next - curr;

                double angle = v1.GetAngleTo(v2);
                double angleDeg = angle * 180.0 / Math.PI;

                if (Math.Abs(angleDeg - 90) > 5) // 允许误差5度
                    return false;
            }
            return true;
        }

        // 计算多段线任意边到点的最短距离
        private double GetDistancePointToPolyline(Polyline pl, Point3d point)
        {
            double minDist = double.MaxValue;
            int vertexCount = pl.NumberOfVertices;

            for (int i = 0; i < vertexCount; i++)
            {
                Point2d pt1 = pl.GetPoint2dAt(i);
                Point2d pt2 = pl.GetPoint2dAt((i + 1) % vertexCount);
                Point2d pt = new Point2d(point.X, point.Y);

                double dist = DistancePointToSegment(pt, pt1, pt2);
                if (dist < minDist)
                    minDist = dist;
            }
            return minDist;
        }

        // 计算点到线段距离（2D）
        private double DistancePointToSegment(Point2d p, Point2d v, Point2d w)
        {
            double l2 = v.GetDistanceTo(w);
            l2 = l2 * l2;
            if (l2 == 0.0) return p.GetDistanceTo(v);

            double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
            t = Math.Max(0, Math.Min(1, t));

            Point2d projection = new Point2d(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
            return p.GetDistanceTo(projection);
        }

        // 获取矩形多段线的长和宽，长为竖直边，宽为水平边
        private void GetRectangleLengthWidth(Polyline pl, out double length, out double width)
        {
            length = 0;
            width = 0;

            if (pl == null || pl.NumberOfVertices != 4) return;

            List<double> edges = new List<double>();
            for (int i = 0; i < 4; i++)
            {
                Point2d pt1 = pl.GetPoint2dAt(i);
                Point2d pt2 = pl.GetPoint2dAt((i + 1) % 4);
                edges.Add(pt1.GetDistanceTo(pt2));
            }

            // 假设两条边长相等，找较长边为length，较短边为width
            double edge1 = edges[0];
            double edge2 = edges[1];

            if (edge1 > edge2)
            {
                length = edge1;
                width = edge2;
            }
            else
            {
                length = edge2;
                width = edge1;
            }
        }


        private sealed class NearbyCircle
        {
            internal readonly Point3d Center;
            internal readonly double Radius;

            internal NearbyCircle(Point3d center, double radius)
            {
                Center = center;
                Radius = radius;
            }
        }

        private void Basepanel_MouseDown6(object sender, MouseEventArgs e)
        {
            //MessageBox.Show("请选择一条直线", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNEw = new ObjectIdCollection();
                pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                  new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectNEw != null && selectNEw.Count > 0)
                {
                    selected = selectNEw;
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                            {
                                ent.Highlight();
                                GlobaltextData3.SelectedEntityId = ent.ObjectId;
                                GlobaltextData3.SelectedEntity = ent;

                                if (GlobaltextData3.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobaltextData3.SelectedEntity;
                                    string entityType = ent1.GetType().Name;
                                    if (ent1 is MText fusetext)
                                    {
                                        Global.fusePoint = fusetext.Location;
                                        Global.fuseTextHeight = fusetext.TextHeight;
                                        MessageBox.Show(fusetext.Text, "内容");

                                        double searchDistance = 20.0; // 搜索半径
                                        Point3d mtextLocation = fusetext.Location;
                                        List<NearbyCircle> nearbyCircles = FindNearbyCircles(database, mtextLocation, searchDistance);

                                        NearbyCircle nearestCircle = null;
                                        double minDistance = double.MaxValue;

                                        // 找到离文字最近的圆
                                        foreach (NearbyCircle circle in nearbyCircles)
                                        {
                                            double distance = mtextLocation.DistanceTo(circle.Center);
                                            if (distance < minDistance)
                                            {
                                                minDistance = distance;
                                                nearestCircle = circle;
                                            }
                                        }

                                        if (nearestCircle != null)
                                        {
                                            Global.circleRadius = nearestCircle.Radius;

                                            List<NearbyCircle> horizontalParallelCircles = new List<NearbyCircle>();
                                            List<NearbyCircle> verticalParallelCircles = new List<NearbyCircle>();

                                            double tolerance = 0.21;//总线型部分不平行差0.2

                                            // 分类平行圆
                                            foreach (NearbyCircle circle in nearbyCircles)
                                            {
                                                if (circle == nearestCircle) continue;

                                                // 水平平行：Y 坐标接近
                                                if (Math.Abs(circle.Center.Y - nearestCircle.Center.Y) < tolerance)
                                                {
                                                    horizontalParallelCircles.Add(circle);
                                                }

                                                // 垂直平行：X 坐标接近
                                                if (Math.Abs(circle.Center.X - nearestCircle.Center.X) < tolerance)
                                                {
                                                    verticalParallelCircles.Add(circle);
                                                }
                                            }

                                            // 初始化最小距离和对应圆
                                            double minHorizontalDistance = double.MaxValue;
                                            double minVerticalDistance = double.MaxValue;
                                            NearbyCircle nearestHorizontalCircle = null;
                                            NearbyCircle nearestVerticalCircle = null;

                                            // 找到最近的水平平行圆
                                            foreach (NearbyCircle circle in horizontalParallelCircles)
                                            {
                                                double dist = nearestCircle.Center.DistanceTo(circle.Center);
                                                if (dist < minHorizontalDistance)
                                                {
                                                    minHorizontalDistance = dist;
                                                    nearestHorizontalCircle = circle;
                                                }
                                            }

                                            // 找到最近的垂直平行圆
                                            foreach (NearbyCircle circle in verticalParallelCircles)
                                            {
                                                double dist = nearestCircle.Center.DistanceTo(circle.Center);
                                                if (dist < minVerticalDistance)
                                                {
                                                    minVerticalDistance = dist;
                                                    nearestVerticalCircle = circle;
                                                }
                                            }

                                            // 最终选择水平或垂直中更近的一组
                                            if (nearestHorizontalCircle != null || nearestVerticalCircle != null)
                                            {
                                                if (nearestVerticalCircle != null &&
                                                    (nearestHorizontalCircle == null || minVerticalDistance < minHorizontalDistance))
                                                {
                                                    Global.circleCenterDistance = minVerticalDistance;
                                                    Global.circleRadius = nearestVerticalCircle.Radius;
                                                    // MessageBox.Show("选中垂直平行圆", "提示");
                                                }
                                                else
                                                {
                                                    Global.circleCenterDistance = minHorizontalDistance;
                                                    Global.circleRadius = nearestHorizontalCircle.Radius;
                                                    // MessageBox.Show("选中水平平行圆", "提示");
                                                }
                                            }
                                            else
                                            {
                                                MessageBox.Show("未找到水平或垂直平行的圆", "提示");
                                            }
                                        }

                                        else
                                        {
                                            MessageBox.Show("未找到附近的保险丝", "提示");
                                        }
                                    }
                                    else if (ent1 is DBText dbtext)
                                    {
                                        // 处理单行文本 (DBText)
                                        Global.fusePoint = dbtext.Position;
                                        Global.fuseTextHeight = dbtext.Height;
                                        MessageBox.Show(dbtext.TextString, "内容");
                                        // 新增逻辑：查找附近的圆形
                                        double searchDistance = 20.0; // 搜索半径
                                        Point3d mtextLocation = dbtext.Position;
                                        List<NearbyCircle> nearbyCircles = FindNearbyCircles(database, mtextLocation, searchDistance);

                                        NearbyCircle nearestCircle = null;
                                        double minDistance = double.MaxValue;

                                        // 找到离文字最近的圆
                                        foreach (NearbyCircle circle in nearbyCircles)
                                        {
                                            double distance = mtextLocation.DistanceTo(circle.Center);
                                            if (distance < minDistance)
                                            {
                                                minDistance = distance;
                                                nearestCircle = circle;
                                            }
                                        }

                                        if (nearestCircle != null)
                                        {
                                            Global.circleRadius = nearestCircle.Radius;

                                            List<NearbyCircle> horizontalParallelCircles = new List<NearbyCircle>();
                                            List<NearbyCircle> verticalParallelCircles = new List<NearbyCircle>();

                                            double tolerance = 0.21;

                                            // 分类平行圆
                                            foreach (NearbyCircle circle in nearbyCircles)
                                            {
                                                if (circle == nearestCircle) continue;

                                                // 水平平行：Y 坐标接近
                                                if (Math.Abs(circle.Center.Y - nearestCircle.Center.Y) < tolerance)
                                                {
                                                    horizontalParallelCircles.Add(circle);
                                                }

                                                // 垂直平行：X 坐标接近
                                                if (Math.Abs(circle.Center.X - nearestCircle.Center.X) < tolerance)
                                                {
                                                    verticalParallelCircles.Add(circle);
                                                }
                                            }

                                            // 初始化最小距离和对应圆
                                            double minHorizontalDistance = double.MaxValue;
                                            double minVerticalDistance = double.MaxValue;
                                            NearbyCircle nearestHorizontalCircle = null;
                                            NearbyCircle nearestVerticalCircle = null;

                                            // 找到最近的水平平行圆
                                            foreach (NearbyCircle circle in horizontalParallelCircles)
                                            {
                                                double dist = nearestCircle.Center.DistanceTo(circle.Center);
                                                if (dist < minHorizontalDistance)
                                                {
                                                    minHorizontalDistance = dist;
                                                    nearestHorizontalCircle = circle;
                                                }
                                            }

                                            // 找到最近的垂直平行圆
                                            foreach (NearbyCircle circle in verticalParallelCircles)
                                            {
                                                double dist = nearestCircle.Center.DistanceTo(circle.Center);
                                                if (dist < minVerticalDistance)
                                                {
                                                    minVerticalDistance = dist;
                                                    nearestVerticalCircle = circle;
                                                }
                                            }

                                            // 最终选择水平或垂直中更近的一组
                                            if (nearestHorizontalCircle != null || nearestVerticalCircle != null)
                                            {
                                                if (nearestVerticalCircle != null &&
                                                    (nearestHorizontalCircle == null || minVerticalDistance < minHorizontalDistance))
                                                {
                                                    Global.circleCenterDistance = minVerticalDistance;
                                                    Global.circleRadius = nearestVerticalCircle.Radius;
                                                    // MessageBox.Show("选中垂直平行圆", "提示");
                                                }
                                                else
                                                {
                                                    Global.circleCenterDistance = minHorizontalDistance;
                                                    Global.circleRadius = nearestHorizontalCircle.Radius;
                                                    // MessageBox.Show("选中水平平行圆", "提示");
                                                }
                                            }
                                            else
                                            {
                                                MessageBox.Show("未找到水平或垂直平行的圆", "提示");
                                            }
                                        }


                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请重新使用此功能", "提示");
                                    }

                                    this.basepanel.MouseDown -= Basepanel_MouseDown6;
                                    break;
                                }

                            }
                        }
                    }

                }
            }
            // 辅助方法：查找附近的圆形
            List<NearbyCircle> FindNearbyCircles(Database database, Point3d mtextLocation, double searchDistance)
            {
                List<NearbyCircle> circles = new List<NearbyCircle>();
                TransactionManager tm = database.TransactionManager;
                using (Transaction trans = tm.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)trans.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId objId in btr)
                    {
                        Entity ent = trans.GetObject(objId, OpenMode.ForRead, false, true) as Entity;
                        if (ent != null && ent is Circle)
                        {
                            Circle circle = (Circle)ent;
                            Point3d center = circle.Center;
                            double distance = mtextLocation.DistanceTo(center); // 计算距离
                            if (distance < searchDistance)
                            {
                                circles.Add(new NearbyCircle(center, circle.Radius)); // 在事务结束前保存需要的数据
                            }
                        }
                    }
                    trans.Commit();
                }
                return circles;
            }
        
        }


        private void Basepanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (bZoomWindow > -1 && bZoomWindow < 2)
            {
                if (bZoomWindow == 1)
                {
                    bZoomWindow = -1;
                    ZoomWindow(firstCornerPoint, toEyeToWorld(e.X, e.Y));
                }
                if (bZoomWindow == 0)
                {
                    firstCornerPoint = toEyeToWorld(e.X, e.Y);
                    bZoomWindow = 1;
                }
            }
            if (!basepanel.Focused)
                basepanel.Focus();
        }

        private void Basepanel_MouseDown(object sender, MouseEventArgs e)
        {
            MouseDownPt = new Point(e.X, e.Y);

            switch (OperModel)
            {
                case Operations.None:
                    #region 获取当前鼠标选中点
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        ObjectIdCollection selectNEw = new ObjectIdCollection();
                        pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5), new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5) }),
                          new SRnew(selectNEw, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                        if (selectNEw != null && selectNEw.Count > 0)
                        {
                            // Selection highlighting only reads entities, including those on locked layers.
                            foreach (ObjectId item in selected)
                            {
                                if (!item.IsNull && !item.IsErased)
                                    using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                                    {
                                        ent.Unhighlight();
                                    }
                            }

                            if (IsBool && EditEnt != null)
                            {
                                EditEnt.setEditEntitys(selectNEw, ref database, ref helperDevice);
                            }
                            //两次选择相同项
                            if (IsSelecttheSame(selected, selectNEw))
                            {
                                #region 移动实体
                                selectedClone = new List<Entity>();
                                foreach (ObjectId item in selectNEw)
                                {
                                    if (!item.IsErased)
                                    {
                                        Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true);
                                        Entity entClone = (Entity)ent.Clone();
                                        entClone.Highlight();
                                        pView.Add(entClone);
                                        selectedClone.Add(entClone);
                                    }
                                }
                                this.Cursor = Cursors.SizeAll;
                                OperModel = Operations.MoveEntity;

                                #endregion
                            }
                            selected = selectNEw;
                            foreach (ObjectId item in selected)
                            {
                                if (!item.IsNull && !item.IsErased)
                                    using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead, false, true))
                                    {
                                        ent.Highlight();
                                    }
                            }
                        }
                        //gripManager.updateSelection(selected);
                        helperDevice.Invalidate();
                        Invalidate();
                    }
                    #endregion
                    break;
                case Operations.Drag:
                    #region 拖拽
                    DragSelected = true;
                    basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.拖拽32白色2.GetHicon());
                    #endregion
                    break;
                case Operations.Roll:
                    break;
                case Operations.Gyroscope:
                    break;
                case Operations.RollEntity:
                    break;
                case Operations.InsertEntity:
                    if (e.Button == MouseButtons.Left)
                    {
                        InsertEntityDowm(e.X, e.Y);
                        DragSelected = true;
                    }
                    break;
                case Operations.PanelDrawPen:
                    List<Point> ListNew = new List<Point>();
                    ListNew.Add(new Point(e.X - 16, e.Y + 16));
                    ListDrwaPen.Add(ListNew);
                    DragSelected = true;
                    break;
                case Operations.SelectFrom:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        selRect = new RectFram(toEyeToWorld(e.X, e.Y));
                        pView.Add(selRect);
                    }
                    break;
                default:
                    break;
            }
        }

        private void Basepanel_MouseMove(object sender, MouseEventArgs e)
        {
            MovePoint = new Point(e.X, e.Y);
            if (database != null && helperDevice != null)
            {
                Point3d Point = toEyeToWorld(e.X, e.Y);
                toolCoordinates.Text = "(X: " + Math.Round(Point.X, 5) + ", Y: " + Math.Round(Point.Y, 5) + ", Z:" + Math.Round(Point.Z, 5) + ")";
            }

            switch (OperModel)
            {
                case Operations.None:
                    break;
                case Operations.Drag:
                    if (DragSelected)
                        Viewer_UDLR(new Point(e.X, e.Y));
                    break;
                case Operations.Roll:
                    break;
                case Operations.MoveEntity:
                    MoverEntitylist_view(new Point(e.X, e.Y));
                    break;
                case Operations.Gyroscope:
                    break;
                case Operations.RollEntity:
                    break;
                case Operations.InsertEntity:
                    InsertEntityMove(e.X, e.Y);
                    break;
                case Operations.PanelDrawPen:
                    if (DragSelected)
                    {
                        if (ListDrwaPen.Count > 0)
                        {
                            ListDrwaPen[ListDrwaPen.Count - 1].Add(new Point(e.X - 16, e.Y + 16));
                            DrawPentrack();
                        }
                    }
                    break;
                case Operations.SelectFrom:
                    if (selRect != null)
                        selRect.setValue(toEyeToWorld(e.X, e.Y));
                    if (helperDevice != null)
                    {
                        helperDevice.Update();
                        Invalidate();
                    }
                    break;
                default:
                    break;
            }

        }

        private void Basepanel_MouseUp(object sender, MouseEventArgs e)
        {
            switch (OperModel)
            {
                case Operations.None:
                    if (DragSelected)
                        DragSelected = false;
                    break;
                case Operations.Drag:
                    DragSelected = false;
                    basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.拖拽32白色.GetHicon());
                    break;
                case Operations.Roll:
                    break;
                case Operations.MoveEntity:
                    Podowns = new Point(0, 0);
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        foreach (Entity item in selectedClone)
                        {
                            if (!item.IsErased)
                            {
                                pView.Erase(item);
                            }
                        }
                    }
                    MoverEntity_view(new Point(e.X, e.Y));
                    this.Cursor = Cursors.Default;
                    OperModel = Operations.None;
                    break;
                case Operations.Gyroscope:
                    break;
                case Operations.RollEntity:
                    break;
                case Operations.InsertEntity:
                    InsertMouseUp(e);
                    break;
                case Operations.PanelDrawPen:
                    DragSelected = false;
                    break;
                case Operations.SelectFrom:
                    if (selRect != null)
                    {
                        #region 批量
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            ObjectIdCollection objection = new ObjectIdCollection();
                            pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X, MouseDownPt.Y), new Point2d(e.X, e.Y) }),
                              new SRnew(objection, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);
                            foreach (ObjectId item in selected)
                            {
                                if (!item.IsNull && !item.IsErased)
                                    using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead))
                                    {
                                        ent.Unhighlight();
                                    }
                            }
                            selected = objection;
                            pView.Erase(selRect);
                            selRect = null;
                            foreach (ObjectId item in selected)
                            {
                                if (!item.IsNull && !item.IsErased)
                                    using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead))
                                    {
                                        ent.Highlight();
                                    }
                            }
                        }
                        #endregion

                        OperModel = Operations.None;
                        basepanel.Cursor = Cursors.Default;
                        toolStripButton9.Checked = false;
                        if (helperDevice != null)
                        {
                            helperDevice.Update();
                            Invalidate();
                        }
                    }
                    break;
                default:
                    break;
            }

        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 移动视图
        /// </summary>
        /// <param name="PoMove"></param>
        private void Viewer_UDLR(Point PoMove)
        {
            double dx = -(PoMove.X - MouseDownPt.X);
            double dy = -(PoMove.Y - MouseDownPt.Y);
            MouseDownPt = new Point(PoMove.X, PoMove.Y);
            using (DBObject pVpObj = active_viewport_id(database).GetObject(OpenMode.ForWrite))
            {
                AbstractViewportData pAVD = new AbstractViewportData(pVpObj);
                Teigha.GraphicsSystem.View pView = pAVD.GsView;

                Vector3d vec = new Vector3d(dx, dy, 0.0);
                vec = vec.TransformBy((pView.ScreenMatrix * pView.ProjectionMatrix).Inverse());
                pView.Dolly(vec);
                pAVD.SetView(pView);
                pAVD.Dispose();
                pVpObj.Dispose();
                Invalidate();
            }

        }

        private void ZoomWindow(Point3d pt1, Point3d pt2)
        {
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                using (AbstractViewPE pVpPE = new AbstractViewPE(pView))
                {
                    pt1 = pt1.TransformBy(pVpPE.WorldToEye);
                    pt2 = pt2.TransformBy(pVpPE.WorldToEye);
                    Vector3d eyeVec = pt2 - pt1;

                    if (((eyeVec.X < -1E-10) || (eyeVec.X > 1E-10)) && ((eyeVec.Y < -1E-10) || (eyeVec.Y > 1E-10)))
                    {
                        Point3d newPos = pt1 + eyeVec / 2.0;
                        pView.Dolly(newPos.GetAsVector());
                        double wf = pView.FieldWidth / Math.Abs(eyeVec.X);
                        double hf = pView.FieldHeight / Math.Abs(eyeVec.Y);
                        pView.Zoom(wf < hf ? wf : hf);
                        Invalidate();
                    }
                }
            }
        }

        private ObjectId active_viewport_id(Database database)
        {
            if (database.TileMode)
            {
                return database.CurrentViewportTableRecordId;
            }
            else
            {
                using (BlockTableRecord paperBTR = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForRead))
                {
                    Layout l = (Layout)paperBTR.LayoutId.GetObject(OpenMode.ForRead);
                    return l.CurrentViewportId;
                }
            }
        }

        private void dolly(Teigha.GraphicsSystem.View pView, int x, int y)
        {
            Vector3d vec = new Vector3d(-x, -y, 0.0);
            vec = vec.TransformBy((pView.ScreenMatrix * pView.ProjectionMatrix).Inverse());
            pView.Dolly(vec);
        }

        private void resize()
        {
            if (helperDevice != null)
            {
                Rectangle r = basepanel.Bounds;
                r.Offset(-basepanel.Location.X, -basepanel.Location.Y);
                // HDC assigned to the device corresponds to the whole client area of the panel
                helperDevice.OnSize(r);
                Invalidate();
            }
        }

        public Point3d toEyeToWorld(int x, int y)
        {
            try
            {
                using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                {
                    Point3d wcsPt = new Point3d(x, y, 0.0);
                    wcsPt = wcsPt.TransformBy((pView.ScreenMatrix * pView.ProjectionMatrix).Inverse());
                    wcsPt = new Point3d(wcsPt.X, wcsPt.Y, 0.0);
                    using (AbstractViewPE pVpPE = new AbstractViewPE(pView))
                    {
                        return wcsPt.TransformBy(pVpPE.EyeToWorld);
                    }
                }
            }
            catch (System.Exception)
            {
            }
            return toEyeToWorld(x, y);
        }

        public void reinitGraphDevice(object sender, Teigha.DatabaseServices.LayoutEventArgs e)
        {
            helperDevice.Dispose();
            graphics.Dispose();
            initializeGraphics();
        }

        private void initializeGraphics()
        {

            try
            {
                graphics = Graphics.FromHwnd(basepanel.Handle);
                // load some predefined rendering module (may be also "WinDirectX" or "WinOpenGL")
                using (GsModule gsModule = (GsModule)SystemObjects.DynamicLinker.LoadModule("WinGDI.txv", false, true))
                {
                    // create graphics device
                    using (Teigha.GraphicsSystem.Device graphichsDevice = gsModule.CreateDevice())
                    {
                        // setup device properties
                        using (Dictionary props = graphichsDevice.Properties)
                        {
                            if (props.Contains("WindowHWND")) // Check if property is supported
                                props.AtPut("WindowHWND", new RxVariant((Int32)basepanel.Handle)); // hWnd necessary for DirectX device
                            if (props.Contains("WindowHDC")) // Check if property is supported
                                props.AtPut("WindowHDC", new RxVariant(graphics.GetHdc())); // hWindowDC necessary for Bitmap device
                            if (props.Contains("DoubleBufferEnabled")) // Check if property is supported
                                props.AtPut("DoubleBufferEnabled", new RxVariant(true));
                            if (props.Contains("EnableSoftwareHLR")) // Check if property is supported
                                props.AtPut("EnableSoftwareHLR", new RxVariant(true));
                            if (props.Contains("DiscardBackFaces")) // Check if property is supported
                                props.AtPut("DiscardBackFaces", new RxVariant(true));
                        }
                        // setup paperspace viewports or tiles
                        Teigha.GraphicsInterface.ContextForDbDatabase ctx = new Teigha.GraphicsInterface.ContextForDbDatabase(database);
                        ctx.UseGsModel = true;

                        helperDevice = LayoutHelperDevice.SetupActiveLayoutViews(graphichsDevice, ctx);
                        //Aux.preparePlotstyles(database, ctx);
                        //helperDevice.ActiveView.Mode = Teigha.GraphicsSystem.RenderMode.HiddenLine;
                    }
                }
                // set palette
                helperDevice.SetLogicalPalette(Device.DarkPalette);
                // set output extents
                resize();


                helperDevice.Model.Invalidate(InvalidationHint.kInvalidateAll);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        void zoom_extents(Teigha.GraphicsSystem.View pView, DBObject pVpObj)
        {
            if (pView == null)
                return;
            // here protocol extension is used again, that provides some helpful functions
            //这里再次使用了协议扩展，提供了一些有用的功能
            using (AbstractViewPE pVpPE = new AbstractViewPE(pView))
            {
                BoundBlock3d bbox = new BoundBlock3d();
                bool bBboxValid = pVpPE.GetViewExtents(bbox);

                // paper space overall view 纸张空间全景
                if (pVpObj is Teigha.DatabaseServices.Viewport && ((Teigha.DatabaseServices.Viewport)pVpObj).Number == 1)
                {
                    if (!bBboxValid || !(bbox.GetMinimumPoint().X < bbox.GetMaximumPoint().X && bbox.GetMinimumPoint().Y < bbox.GetMaximumPoint().Y))
                    {
                        bBboxValid = get_layout_extents(database, pView, ref bbox);
                    }
                }

                else if (!bBboxValid) // model space viewport 模型空间窗口
                {
                    bBboxValid = get_layout_extents(database, pView, ref bbox);
                }

                if (!bBboxValid)
                {
                    // set to somewhat reasonable (e.g. paper size) 设置为比较合理(例如纸张大小)
                    if (database.Measurement == MeasurementValue.Metric)
                    {
                        bbox.Set(Point3d.Origin, new Point3d(297.0, 210.0, 0.0)); // set to papersize ISO A4 (portrait)
                    }
                    else
                    {
                        bbox.Set(Point3d.Origin, new Point3d(11.0, 8.5, 0.0)); // ANSI A (8.50 x 11.00) (landscape)
                    }
                    bbox.TransformBy(pView.ViewingMatrix);
                }

                pVpPE.ZoomExtents(bbox);
            }
        }

        bool get_layout_extents(Database db, Teigha.GraphicsSystem.View pView, ref BoundBlock3d bbox)
        {
            BlockTable bt = (BlockTable)db.BlockTableId.GetObject(OpenMode.ForRead);
            BlockTableRecord pSpace = (BlockTableRecord)bt[BlockTableRecord.PaperSpace].GetObject(OpenMode.ForRead);
            Layout pLayout = (Layout)pSpace.LayoutId.GetObject(OpenMode.ForRead);
            Extents3d ext = new Extents3d();
            if (pLayout.GetViewports().Count > 0)
            {
                bool bOverall = true;
                foreach (ObjectId id in pLayout.GetViewports())
                {
                    if (bOverall)
                    {
                        bOverall = false;
                        continue;
                    }

                    Teigha.DatabaseServices.Viewport pVp = (Teigha.DatabaseServices.Viewport)id.GetObject(OpenMode.ForRead);
                }
                ext.TransformBy(pView.ViewingMatrix);
                bbox.Set(ext.MinPoint, ext.MaxPoint);
            }
            else
            {
                ext = pLayout.Extents;
            }
            bbox.Set(ext.MinPoint, ext.MaxPoint);
            return ext.MinPoint != ext.MaxPoint;
        }

        public bool IsSelecttheSame(ObjectIdCollection Objitem1, ObjectIdCollection Objitem2)
        {
            if (Objitem1.Count == Objitem2.Count)
            {
                for (int i = 0; i < Objitem1.Count; i++)
                {
                    if (Objitem1[i] != Objitem2[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        public void DWF_export(Database database, Teigha.GraphicsSystem.LayoutHelperDevice helperDevice)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.AddExtension = true;
            sfd.DefaultExt = "dwf";
            sfd.Title = "Export To Dwf";
            sfd.Filter = String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
              "Binary DWF (Desgin Web Format) v6.0 (*.dwf)|*.dwf",
              "Zipped Ascii Encoded 2D Stream DWF (Design Web Format) v6.0 (*.dwf)|*.dwf",
              "Compressed DWF (Design Web Format) v5.5 (*.dwf)|*.dwf",
              "Binary DWF (Design Web Format) v5.5 (*.dwf)|*.dwf",
              "Ascii DWF (Design Web Format) v5.5 (*.dwf)|*.dwf",
              "Compressed DWF (Design Web Format) v4.2 (*.dwf)|*.dwf",
              "Binary DWF (Design Web Format) v4.2 (*.dwf)|*.dwf",
              "Ascii DWF (Design Web Format) v4.2 (*.dwf)|*.dwf");
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                String strFileName = sfd.FileName;
                using (mDwfExportParams param = new mDwfExportParams())
                {
                    param.Database = database;
                    param.FileName = strFileName;
                    param.BackgroundColor = helperDevice.BackgroundColor;
                    param.InkedArea = false;
                    param.ColorMapOptimize = false;
                    param.ExportInvisibleText = true;
                    DwfPageDataCollection pageDataColl = new DwfPageDataCollection();
                    DWFPageData pageData;
                    switch (sfd.FilterIndex)
                    {
                        case 1:
                            param.Format = DwfFormat.UNCOMPRESSED_BINARY;
                            param.Version = DwfVersion.Dwf_v60;

                            pageData = new DWFPageData();
                            pageData.Layout = LayoutManager.Current.CurrentLayout;
                            pageDataColl.Add(pageData);
                            param.PageData = pageDataColl;
                            break;
                        case 2:
                            param.Format = DwfFormat.ASCII;
                            param.Version = DwfVersion.Dwf_v60;

                            pageData = new DWFPageData();
                            pageData.Layout = LayoutManager.Current.CurrentLayout;
                            pageDataColl.Add(pageData);
                            param.PageData = pageDataColl;
                            break;
                        case 3:
                            param.Format = DwfFormat.COMPRESSED_BINARY;
                            param.Version = DwfVersion.Dwf_v55;
                            break;
                        case 4:
                            param.Format = DwfFormat.UNCOMPRESSED_BINARY;
                            param.Version = DwfVersion.Dwf_v55;
                            break;
                        case 5:
                            param.Format = DwfFormat.ASCII;
                            param.Version = DwfVersion.Dwf_v55;
                            break;
                        case 6:
                            param.Format = DwfFormat.COMPRESSED_BINARY;
                            param.Version = DwfVersion.Dwf_v42;
                            break;
                        case 7:
                            param.Format = DwfFormat.UNCOMPRESSED_BINARY;
                            param.Version = DwfVersion.Dwf_v42;
                            break;
                        case 8:
                            param.Format = DwfFormat.ASCII;
                            param.Version = DwfVersion.Dwf_v42;
                            break;
                        default:
                            return;
                    }
                    Export_Import.ExportDwf(param);
                }
            }
        }

        #endregion

        #region 公共方法
        /// <summary>
        /// 初始化Dwg并显示 无进度条
        /// </summary>
        /// <param name="Paths"></param>
        public void file_open_handlerNotBar(object Paths)
        {
            DwgPath = Paths.ToString();
            if (System.IO.File.Exists(Paths.ToString()))
            {
                if (lm != null)
                {

                    lm.LayoutSwitched -= new Teigha.DatabaseServices.LayoutEventHandler(reinitGraphDevice);
                    HostApplicationServices.WorkingDatabase = null;
                    lm = null;
                }
                bool bLoaded = true;
                database = new Database(false, false);
                if (System.IO.Path.GetExtension(Paths.ToString()).ToLower() == ".dwg"
                 || System.IO.Path.GetExtension(Paths.ToString()).ToLower() == ".tmp")
                {
                    try
                    {
                        database.ReadDwgFile(Paths.ToString(), FileOpenMode.OpenForReadAndAllShare, false, "");

                    }
                    catch (System.Exception ex)
                    {
                        KryptonMessageBox.Show("打开失败" + Environment.NewLine + "错误原因：" + "图纸版本过高 "/*ex.Message*/);
                        //.Show(ex.Message);
                        database.ReadDwgFile(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + @"Template\dwgModel.dwg", FileOpenMode.OpenForReadAndAllShare, false, "");
                        //bLoaded = false;
                    }
                }
                else if (System.IO.Path.GetExtension(Paths.ToString()).ToLower() == ".dxf")
                {
                    try
                    {
                        database.DxfIn(Paths.ToString(), "");
                    }
                    catch (System.Exception ex)
                    {
                        KryptonMessageBox.Show("打开失败" + Environment.NewLine + "错误原因：" + ex.Message);
                        database.ReadDwgFile(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + @"Template\dwgModel.dwg", FileOpenMode.OpenForReadAndAllShare, false, "");
                        //MessageBox.Show(ex.Message);
                        //bLoaded = false;
                    }
                }
                if (bLoaded)
                {
                    //设置当前活动数据库
                    HostApplicationServices.WorkingDatabase = database;
                    lm = LayoutManager.Current;
                    lm.LayoutSwitched += new Teigha.DatabaseServices.LayoutEventHandler(reinitGraphDevice);
                    String str = HostApplicationServices.Current.FontMapFileName;
                    this.Text = String.Format("[{0}]", System.IO.Path.GetFileName(Paths.ToString()));
                    this.Tag = Paths;
                    initializeGraphics();

                    this.Invalidate();
                    this.Focus();
                }
            }
        }


        #endregion

        #region 览图事件

        /// <summary>
        /// Dwg 自适应内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void zoom_extents_handler()
        {
            using (DBObject pVpObj = active_viewport_id(database).GetObject(OpenMode.ForWrite))
            {
                // using protocol extensions we handle PS and MS viewports in the same manner 使用协议扩展，我们以相同的方式处理PS和MS视图
                AbstractViewportData pAVD = new AbstractViewportData(pVpObj);
                Teigha.GraphicsSystem.View pView = pAVD.GsView;
                if (pView != null)
                {
                    // do actual zooming - change GS view 实际缩放-改变GS视图
                    zoom_extents(pView, pVpObj);
                    // save changes to database 保存对数据库的更改
                    pAVD.SetView(pView);
                    pAVD.Dispose();
                    pVpObj.Dispose();
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 缩放
        /// </summary>
        /// <param name="num"></param>
        public void Set_zoom(double num)
        {
            try
            {
                using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                {  // camera position in world coordinates
                    Point3d pos = pView.Position;
                    // TransformBy() returns a transformed copy
                    pos = pos.TransformBy(pView.WorldToDeviceMatrix);
                    int vx = (int)pos.X;
                    int vy = (int)pos.Y;
                    vx = MovePoint.X - vx;
                    vy = MovePoint.Y - vy;
                    // we move point of view to the mouse location, to create an illusion of scrolling in/out there
                    dolly(pView, -vx, -vy);
                    pView.Zoom(num);
                    dolly(pView, vx, vy);
                    nm1 *= num;
                    toolStripLabel1.Text = (int)nm1+"%";
                    //
                    ReceiverViews receive = new ReceiverViews(num, pos, MovePoint, ref helperDevice);
                    //ReceiverView Receive = new ReceiverView(new Point(MovePoint.X, MovePoint.Y), new Point((int)pos.X, (int)pos.Y), num, ref database, ref helperDevice);
                    Invoker.SetCommad(receive);
                    
                    Invalidate();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region 图层
        /// <summary>
        /// 关闭选中
        /// </summary>
        public void LayerHidePitchOn()
        {
            using (var trans = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId item in selected)
                {
                    if (!item.IsNull && !item.IsErased)
                    {
                        Entity ent = item.GetObject(OpenMode.ForRead) as Entity;
                        LayerTableRecord LayerEnt = trans.GetObject(ent.LayerId, OpenMode.ForWrite) as LayerTableRecord;
                        LayerEnt.IsOff = true;
                    }
                }
                trans.Commit();
            }
            helperDevice.Invalidate();
            Invalidate();
        }

        /// <summary>
        /// 关闭除选中之外的图层
        /// </summary>
        public void LayerHideExceptPitchOn()
        {
            using (var trans = database.TransactionManager.StartTransaction())
            {
                List<ObjectId> PutchonObj = new List<ObjectId>();
                foreach (ObjectId item in selected)
                {
                    if (!item.IsNull && !item.IsErased)
                    {
                        Entity ent = item.GetObject(OpenMode.ForWrite) as Entity;
                        PutchonObj.Add(ent.LayerId);
                    }
                }
                LayerTable LatTab = trans.GetObject(database.LayerTableId, OpenMode.ForWrite) as LayerTable;
                foreach (ObjectId item in LatTab)
                {
                    if (PutchonObj.Where(s1 => { return s1 == item; }).ToList().Count() == 0)
                    {
                        LayerTableRecord LayerEnt = trans.GetObject(item, OpenMode.ForWrite) as LayerTableRecord;
                        LayerEnt.IsOff = true;
                    }
                }

                trans.Commit();
            }
            helperDevice.Invalidate();
            Invalidate();

        }

        /// <summary>
        /// 打开所有图层
        /// </summary>
        public void LayerShowAll()
        {
            using (var trans = database.TransactionManager.StartTransaction())
            {

                LayerTable LatTab = trans.GetObject(database.LayerTableId, OpenMode.ForWrite) as LayerTable;
                foreach (ObjectId item in LatTab)
                {
                    LayerTableRecord LayerEnt = trans.GetObject(item, OpenMode.ForWrite) as LayerTableRecord;
                    LayerEnt.IsOff = false;
                }
                trans.Commit();
            }
            helperDevice.Invalidate();
            Invalidate();
        }

        #endregion

        #region 移动实体
        /// <summary>
        /// 中间过渡坐标点
        /// </summary>
        Point Podowns = new Point();
        private void MoverEntitylist_view(Point PoMove)
        {
            Point3d Podown1 = new Point3d();
            Point3d Pomove1 = new Point3d();
            double dx = 0;
            double dy = 0;
            if (Podowns == new Point(0, 0))
            {
                Podown1 = toEyeToWorld(MouseDownPt.X, MouseDownPt.Y);
                Pomove1 = toEyeToWorld(PoMove.X, PoMove.Y);
                dx = Pomove1.X - Podown1.X;
                dy = Pomove1.Y - Podown1.Y;
                Podowns = PoMove;
            }
            else
            {
                Podown1 = toEyeToWorld(Podowns.X, Podowns.Y);
                Pomove1 = toEyeToWorld(PoMove.X, PoMove.Y);
                dx = Pomove1.X - Podown1.X;
                dy = Pomove1.Y - Podown1.Y;
                Podowns = PoMove;
            }
            //防止重复移动
            List<Entity> Listitem = new List<Entity>();
            using (var trans = database.TransactionManager.StartTransaction())
            {
                foreach (Entity item in selectedClone)
                {
                    if (Listitem.FindIndex(s1 => s1 == item) == -1)
                    {
                        Listitem.Add(item);
                        //移动
                        Point3d Pointnew = new Point3d(dx, dy, 0);
                        Matrix3d mat = Matrix3d.Displacement(Pointnew.GetAsVector());
                        item.TransformBy(mat);
                    }
                }
                trans.Commit();
            }
            Listitem.Clear();
            if (helperDevice != null)
                helperDevice.Update();
            helperDevice.Invalidate();
            Invalidate();
        }

        /// <summary>
        /// 画实体移动
        /// </summary>
        public void MoveEntity()
        {
            try
            {
                using (Graphics g = basepanel.CreateGraphics())
                {
                    //设置高质量插值法    
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    //设置高质量,低速度呈现平滑程度    
                    //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    //消除锯齿  
                    //g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PageUnit = GraphicsUnit.Pixel;


                    Pen penMultiLine = new Pen(Color.White, 2); //在每个点上画延长线用的
                                                                //penMultiLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                                                                //penMultiLine.DashPattern = new float[] { 5, 5 };
                    penMultiLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    g.DrawImage(imageList1.Images[2], new PointF(MouseDownPt.X - 16, MouseDownPt.Y - 16));
                    g.DrawLine(penMultiLine, MouseDownPt, MovePoint); //显示中心点坐标

                    g.Save();
                }
            }
            catch { }
        }

        /// <summary>
        /// 移动实体
        /// </summary>
        /// <param name="PoMove"></param>
        private void MoverEntity_view(Point PoMove)
        {
            Point3d Podown1 = toEyeToWorld(MouseDownPt.X, MouseDownPt.Y);
            Point3d Pomove1 = toEyeToWorld(PoMove.X, PoMove.Y);
            double dx = Pomove1.X - Podown1.X;
            double dy = Pomove1.Y - Podown1.Y;

            if (dx == 0 && dy == 0)
                return;

            //防止重复移动
            List<ObjectId> Listitem = new List<ObjectId>();

            List<Entity> entold = new List<Entity>();
            List<Entity> newold = new List<Entity>();
            ObjectIdCollection oldids = new ObjectIdCollection();
            ObjectIdCollection newids = new ObjectIdCollection();
            using (var trans = database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId item in selected)
                {
                    if (Listitem.FindIndex(s1 => s1 == item) == -1)
                    {
                        Listitem.Add(item);
                        if (!item.IsErased && item.IsValid)
                        {
                            using (Entity Ent = item.GetObject(OpenMode.ForWrite) as Entity)
                            {
                                Entity oldent = (Entity)Ent.Clone();
                                oldids.Add(Ent.Id);
                                newids.Add(Ent.Id);
                                entold.Add(oldent);
                                //Objids.Add(item);
                                //OldEnts.Add((Entity)Ent.Clone());
                                //移动
                                Point3d Pointnew = new Point3d(dx, dy, 0);
                                Matrix3d mat = Matrix3d.Displacement(Pointnew.GetAsVector());
                                Ent.TransformBy(mat);
                                Entity newent = (Entity)Ent.Clone();
                                newold.Add(newent);


                            }
                        }
                    }
                }
                if (selected.Count > 0)
                {
                    ReceiverSelection entCommand = new ReceiverSelection(oldids, newids, entold, newold, ref database, ref helperDevice);
                    Invoker.SetCommad(entCommand);
                }
                trans.Commit();
            }

            Listitem.Clear();
            if (helperDevice != null)
                helperDevice.Update();
            helperDevice.Invalidate();
            Invalidate();

            //foreach (ObjectId item in Objids)
            //{
            //    NewEnts.Add((Entity)item.GetObject(OpenMode.ForRead, false, false).Clone());
            //}

            //ReceiverSelection Eecesect = new ReceiverSelection(Objids, OldEnts, NewEnts, ref database, ref helperDevice);
            //InvokerCom.SetCommad(Eecesect);
        }
        #endregion

        #region 功能栏事件
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (database != null)
            {
                if (DialogResult.OK == saveAsFileDialog.ShowDialog(this))
                {
                    database.SaveAs(saveAsFileDialog.FileName, DwgVersion.Current);
                }
            }
        }
        public static class Global
        {
            public static double x1;
            public static double y1;
            public static double x2;
            public static double y2;
            public static List<double> mlist = new List<double>(4);

            // 添加屏幕坐标的全局变量
            public static int screenX1;
            public static int screenY1;
            public static int screenX2;
            public static int screenY2;

            public static bool drawRectangle = false;

            //电气图部分初始信息
            public static double dk_height;//引脚文字高度
            public static double qj_height;//器件文字高度
            public static double jd_height;
            public static Line fline = null;
            public static double area;//保险丝矩形面积
            public static Point3d fusePoint;
            public static double fuseTextHeight;
            //public static double width;//矩形框的宽
            public static double circleRadius;//保险丝圆形半径
            public static double jdqrectangleLength;
            public static double jdqrectangleWidth;
            public static double circleCenterDistance;//保险丝圆心距离
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (database != null)
            {
                database.SaveAs(DwgPath, DwgVersion.Current);
            }
        }

        EditEntitys EditEnt = null;
        bool IsBool = false;
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.十字光标.GetHicon());
            EditEnt = new EditEntitys();
            EditEnt.TopLevel = false;
            EditEnt.Dock = DockStyle.Fill;
            EditEnt.Activate();
            EditEnt.setEditEntitys(selected, ref database, ref helperDevice);
            //DBForm.ContextMenuStrip = contextMenu;
            EditEnt.FormBorderStyle = FormBorderStyle.None;
            splitContainer1.Panel2Collapsed = false;
            splitContainer1.Panel2.Controls.Add(EditEnt);
            EditEnt.Show();
            IsBool = true;
            /*
            if (toolStripButton3.Checked)
            {
                basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.十字光标.GetHicon());
                EditEnt = new EditEntitys();
                EditEnt.TopLevel = false;
                EditEnt.Dock = DockStyle.Fill;
                EditEnt.Activate();
                EditEnt.setEditEntitys(selected, ref database, ref helperDevice);
                //DBForm.ContextMenuStrip = contextMenu;
                EditEnt.FormBorderStyle = FormBorderStyle.None;
                splitContainer1.Panel2Collapsed = false;
                splitContainer1.Panel2.Controls.Add(EditEnt);
                EditEnt.Show();
                IsBool = true;
            }
            else
            {
                
                if (EditEnt != null)
                {
                    //EditEnt.ClearView();
                    EditEnt.Close();
                    EditEnt = null;
                }
                IsBool = false;
                splitContainer1.Panel2Collapsed = true;
                basepanel.Cursor = Cursors.Default;
                
            }
            */
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {

            if (database != null)
            {
                using (var trans = database.TransactionManager.StartTransaction())
                {
                    List<Entity> entold = new List<Entity>();
                    ObjectIdCollection oldids = new ObjectIdCollection();
                    ObjectIdCollection newids = new ObjectIdCollection();
                    foreach (ObjectId item in selected)
                    {
                        if (!item.IsErased && item.IsValid)
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForWrite, false, false))
                            {
                                ent.Erase(true);
                                oldids.Add(ent.Id);
                                newids.Add(new ObjectId());
                                entold.Add((Entity)ent.Clone());

                            }
                    }
                    if (selected.Count > 0)
                    {
                        ReceiverSelection receSelection = new ReceiverSelection(oldids, newids, entold, new List<Entity>(), ref database, ref helperDevice);
                        Invoker.SetCommad(receSelection);
                        selected = new ObjectIdCollection();
                    }
                    trans.Commit();
                }
            }
            if (helperDevice != null)
            {
                helperDevice.Update();
                Invalidate();
            }
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            if (toolStripButton9.Checked)
            {
                OperModel = Operations.SelectFrom;
                basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.十字光标.GetHicon());
            }
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                colorAll = colorDialog1.Color;
            }
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            DWF_export(database, helperDevice);
        }

        /// <summary>
        /// 向后
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            Invoker.UndoStack();
        }

        /// <summary>
        /// 向前
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            Invoker.RedoStack();
        }

        private void Invoker_ChangeStack()
        {
            toolStripButton8.Text = "撤销(" + Invoker.undoStack.Count.ToString() + ")";
            toolStripButton7.Text = "执行(" + Invoker.redoStack.Count.ToString() + ")";
            if (Invoker.undoStack.Count == 0)
                toolStripButton8.Enabled = false;
            else
                toolStripButton8.Enabled = true;

            if (Invoker.redoStack.Count == 0)
                toolStripButton7.Enabled = false;
            else
                toolStripButton7.Enabled = true;
        }
        #endregion

        #region 界面绘图
        private void DrawPentrack()
        {
            //以为刚开始时，角度和长度可能为o，故用try防止报错
            try
            {
                using (Graphics g = basepanel.CreateGraphics())
                {
                    //设置高质量插值法    
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    //设置高质量,低速度呈现平滑程度    
                    //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    //消除锯齿  
                    //g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PageUnit = GraphicsUnit.Pixel;


                    Pen penMultiLine = new Pen(colorAll, 2); //在每个点上画延长线用的
                                                             //penMultiLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                                                             //penMultiLine.DashPattern = new float[] { 5, 5 };
                    for (int i = 0; i < ListDrwaPen.Count; i++)
                    {
                        g.DrawLines(penMultiLine, ListDrwaPen[i].ToArray());
                    }
                }
            }
            catch { }
        }

        private void DrawArcs()
        {
            //以为刚开始时，角度和长度可能为o，故用try防止报错
            try
            {
                using (Graphics g = basepanel.CreateGraphics())
                {
                    //设置高质量插值法    
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    //设置高质量,低速度呈现平滑程度    
                    //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    //消除锯齿  
                    //g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PageUnit = GraphicsUnit.Pixel;


                    Pen penMultiLine = new Pen(Color.White, 2); //在每个点上画延长线用的
                                                                //penMultiLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                                                                //penMultiLine.DashPattern = new float[] { 5, 5 };

                    double Angle = Function.Angle(MouseDownPt, new Point(MouseDownPt.X + 20, MouseDownPt.Y), MovePoint);
                    double Length = Function.dis(toEyeToWorld(MouseDownPt.X, MouseDownPt.Y), toEyeToWorld(MovePoint.X, MovePoint.Y));
                    g.DrawLine(penMultiLine, MouseDownPt, MovePoint); //画半径线
                    if (MovePoint.Y >= MouseDownPt.Y)
                    {
                        if (!double.TryParse(Angle.ToString(), out Angle) || double.IsNaN(Angle))
                            Angle = 0;
                        _RollAre = (float)(360 - Angle);
                    }
                    else
                    {
                        if (!double.TryParse(Angle.ToString(), out Angle) || double.IsNaN(Angle))
                            Angle = 0;
                        _RollAre = (float)(Angle);
                    }

                    Font FtX = new Font("Arial", 8, FontStyle.Bold); //Ｘ轴的字做小一点
                    Brush FtBrush = new SolidBrush(Color.White); //字体用的笔刷
                    //Rotate_Angle = DouAngle;
                    //显示角度
                    g.DrawString("角度：" + _RollAre.ToString("F3") + "°" + Environment.NewLine + "半径:" + Length.ToString("F3"), FtX, FtBrush, new Point(MovePoint.X + 10, MovePoint.Y));
                    g.Save();
                }
            }
            catch { }
        }

        private void DrawCircle()
        {
            //以为刚开始时，角度和长度可能为o，故用try防止报错
            try
            {
                using (Graphics g = basepanel.CreateGraphics())
                {
                    //设置高质量插值法    
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    //设置高质量,低速度呈现平滑程度    
                    //g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                    //消除锯齿  
                    //g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PageUnit = GraphicsUnit.Pixel;


                    Pen penMultiLine = new Pen(Color.White, 2); //在每个点上画延长线用的
                                                                //penMultiLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                                                                //penMultiLine.DashPattern = new float[] { 5, 5 };

                    double Length = Math.Round(Function.dis(toEyeToWorld(MouseDownPt.X, MouseDownPt.Y), toEyeToWorld(MovePoint.X, MovePoint.Y)), 4);
                    g.DrawLine(penMultiLine, MouseDownPt, MovePoint); //画半径线

                    Font FtX = new Font("Arial", 8, FontStyle.Bold); //Ｘ轴的字做小一点
                    Brush FtBrush = new SolidBrush(Color.White); //字体用的笔刷
                    //Rotate_Angle = DouAngle;
                    //显示角度
                    g.DrawString(" " + Length.ToString(), FtX, FtBrush, new Point(MovePoint.X + 10, MovePoint.Y));
                    g.Save();
                }
            }
            catch { }
        }

        #endregion

        #region 插入实体
        /// <summary>
        /// 是否处于选中状态，禁止界面的缩放
        /// </summary>
        public bool DragSelected = false;

        /// <summary>
        /// 临时存放实体类
        /// </summary>
        public Entity TemporaryEnt = null;

        public EntityType EntType = EntityType.None;

        /// <summary>
        /// 角度值
        /// </summary>
        double _RollAre = 0;

        List<List<Point>> ListDrwaPen = new List<List<Point>>();
        /// <summary>
        /// 画笔
        /// </summary>
        /// <param name="bol"></param>
        public void DrawPebs(bool bol = true)
        {
            if (bol)
            {
                ListDrwaPen = new List<List<Point>>();
                OperModel = Operations.PanelDrawPen;
                basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.画笔_1_.GetHicon());
            }
            else
            {
                ListDrwaPen = new List<List<Point>>();
                OperModel = Operations.None;
                basepanel.Cursor = Cursors.Default;
                DragSelected = false;
                this.Invalidate();
            }
        }

        /// <summary>
        /// 插入实体
        /// </summary>
        public void InsertLine(bool bol, EntityType _EntType)//控制绘制操作的开启与关闭，具体是插入特定类型的实体（例如直线、弧线等）
        {
            OperModel = bol ? Operations.InsertEntity : Operations.None;
            EntType = bol ? _EntType : EntityType.None;
            basepanel.Cursor = bol ? new System.Windows.Forms.Cursor(Properties.Resources.十字光标.GetHicon()) : Cursors.Default;
        }

        //根据当前 EntType 实体类型，在 CAD 图形视图中插入指定的图形对象或实体。
        private void InsertEntityDowm(int X, int Y)
        {
            switch (EntType)
            {
                case EntityType.Line:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        Line line = new Line(toEyeToWorld(X, Y), toEyeToWorld(X, Y));
                        TemporaryEnt = line;
                        TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                        pView.Add(TemporaryEnt);
                    }
                    break;
                case EntityType.Polyline:
                    if (TemporaryEnt == null)
                    {
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {

                            Polyline polyline = new Polyline();
                            polyline.SetDatabaseDefaults(database);
                            polyline.AddVertexAt(0, new Point2d(toEyeToWorld(X, Y).X, toEyeToWorld(X, Y).Y), 0, 0, 0); // 添加第一个点
                            TemporaryEnt = polyline; // 设置为临时实体
                            TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                            pView.Add(TemporaryEnt);


                        }
                    }
                    else
                    {

                        // 确保 TemporaryEnt 是 Mline 类型
                        //if (TemporaryEnt is Mline mline)
                        if (TemporaryEnt is Polyline polyline)
                        {
                            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                            {
                                //Point3d currentPoint = toEyeToWorld(X, Y);
                                ////Point3d firstPoint = mline.VertexAt(0); // 起始点
                                //Point2d firstPoint = polyline.GetPoint2dAt(0); // 起始点
                                //// 检查当前点是否接近起始点，如果接近则闭合多边形
                                ////if (currentPoint.DistanceTo(firstPoint) < 0.5)
                                //if (currentPoint.DistanceTo(new Point3d(firstPoint.X, firstPoint.Y, 0)) < 1)
                                //{
                                //    //mline.AppendSegment(firstPoint); // 闭合多边形
                                //    polyline.Closed = true; // 闭合多边形
                                //    pView.Erase(TemporaryEnt);
                                //    pView.Add(TemporaryEnt);

                                //    // 将包围盒添加到全局变量
                                //    mergedPolylines.Add(polyline);

                                //    TemporaryEnt = null; // 重置临时实体
                                //    InsertLine(false, EntityType.None); // 重置绘制模式







                                Point3d currentPoint = toEyeToWorld(X, Y);
                                //Point3d firstPoint = mline.VertexAt(0); // 起始点
                                Point2d firstPoint = polyline.GetPoint2dAt(0); // 起始点
                                // 检查当前点是否接近起始点，如果接近则闭合多边形
                                //if (currentPoint.DistanceTo(firstPoint) < 0.5)
                                if (currentPoint.DistanceTo(new Point3d(firstPoint.X, firstPoint.Y, 0)) < 1)
                                {
                                    //mline.AppendSegment(firstPoint); // 闭合多边形
                                    polyline.Closed = true; // 闭合多边形
                                    pView.Erase(TemporaryEnt);
                                    pView.Add(TemporaryEnt);

                                    // 创建一个新的 PolylineWithIndex 并为其分配序号
                                    PolylineWithIndex polylineWithIndex = new PolylineWithIndex
                                    {
                                        Polyline = polyline,
                                        Index = mergedPolylinesWithIndex.Count + 1  // 按顺序分配序号
                                    };

                                    // 将此 PolylineWithIndex 存储到全局变量
                                    mergedPolylinesWithIndex.Add(polylineWithIndex);

                                    // 将包围盒添加到全局变量
                                    //mergedPolylines.Add(polyline);

                                    TemporaryEnt = null; // 重置临时实体
                                    InsertLine(false, EntityType.None); // 重置绘制模式










                                }
                                else
                                {
                                    // 添加新顶点
                                    //mline.AppendSegment(currentPoint);
                                    polyline.AddVertexAt(polyline.NumberOfVertices, new Point2d(currentPoint.X, currentPoint.Y), 0, 0, 0);
                                }
                            }
                        }
                    }
                    break;
                case EntityType.Arc:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        Arc arc = new Arc();
                        arc.Center = toEyeToWorld(X, Y);
                        arc.Radius = 0;
                        arc.StartAngle = 0.0;
                        arc.EndAngle = 0.0;
                        TemporaryEnt = arc;
                        TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                        pView.Add(TemporaryEnt);
                    }
                    _RollAre = 0;
                    DrawArcs();
                    break;
                case EntityType.Circle:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        Circle cir = new Circle();
                        cir.Center = toEyeToWorld(X, Y);
                        cir.Radius = 0;
                        TemporaryEnt = cir;
                        TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                        pView.Add(TemporaryEnt);
                    }
                    break;
                case EntityType.Point:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        DBPoint Dbmine = new DBPoint();
                        Dbmine.Thickness = 5;
                        Dbmine.Position = toEyeToWorld(X, Y);
                        TemporaryEnt = Dbmine;
                        TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                        pView.Add(TemporaryEnt);
                    }
                    break;
                case EntityType.Ellipse:
                    //using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    //{
                    //Ellipse ellipse = new Ellipse();
                    //ellipse.Center = toEyeToWorld(X, Y);
                    //ellipse.RadiusRatio = 0.5;
                    //ellipse.set_StartParam(10);
                    ////ellipse.Set(toEyeToWorld(X, Y), new Vector3d(0, 0, 1), new Vector3d(10, 0, 0), 0.5, 0, 0);
                    //TemporaryEnt = ellipse;
                    //TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                    //pView.Add(TemporaryEnt);
                    //}
                    break;
                case EntityType.None:
                    break;
                case EntityType.leader:
                    if (TemporaryEnt == null)
                    {
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            Leader pLeader = new Leader();
                            pLeader.SetDatabaseDefaults(database);
                            pLeader.AppendVertex(toEyeToWorld(X, Y));
                            pLeader.AppendVertex(toEyeToWorld(X, Y));
                            TemporaryEnt = pLeader;
                            TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                            pView.Add(TemporaryEnt);
                        }

                    }
                    else
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                string strtext = KryptonInputBox.Show("请输入注释内容！", "提示");
                                if (!string.IsNullOrWhiteSpace(strtext))
                                {
                                    ObjectId objitem = block.AppendEntity((Entity)TemporaryEnt.Clone());
                                    using (Entity ent = (Entity)objitem.GetObject(OpenMode.ForWrite, false))
                                    {

                                        Leader Pleader = (Leader)ent;
                                        MText pMText = new MText();
                                        pMText.SetDatabaseDefaults(database);
                                        ObjectId mTextId = block.AppendEntity(pMText);
                                        double textHeight = 0.15;
                                        double textWidth = 1.0;
                                        pMText.Location = Pleader.VertexAt(Pleader.NumVertices - 1);
                                        pMText.TextHeight = textHeight;
                                        pMText.Width = textWidth;
                                        pMText.Attachment = AttachmentPoint.MiddleLeft;
                                        pMText.Contents = strtext;
                                        Pleader.Annotation = mTextId;

                                        List<Entity> entold = new List<Entity>();
                                        List<Entity> newold = new List<Entity>();
                                        ObjectIdCollection oldids = new ObjectIdCollection();
                                        ObjectIdCollection newids = new ObjectIdCollection();
                                        oldids.Add(ObjectId.Null);
                                        oldids.Add(ObjectId.Null);
                                        newids.Add(mTextId);
                                        newids.Add(objitem);
                                        newold.Add((Entity)pMText.Clone());
                                        newold.Add((Entity)Pleader.Clone());

                                        ReceiverSelection receSelection = new ReceiverSelection(oldids, newids, entold, newold, ref database, ref helperDevice);
                                        Invoker.SetCommad(receSelection);
                                    }


                                }
                                //block.AppendEntity((Entity)TemporaryEnt.Clone());
                            }
                            trans.Commit();
                        }
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            pView.Erase(TemporaryEnt);
                        }
                        TemporaryEnt = null;
                        InsertLine(false, EntityType.None);
                    }
                    break;
                case EntityType.LineDim:
                    if (TemporaryEnt == null)
                    {
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            //AlignedDimension pDimension = new AlignedDimension();
                            //pDimension.SetDatabaseDefaults(database);
                            //pDimension.XLine1Point = toEyeToWorld(X, Y);
                            //pDimension.XLine2Point = toEyeToWorld(MovePoint.X, MovePoint.Y);
                            //pDimension.UsingDefaultTextPosition = true;
                            //TemporaryEnt = pDimension;
                            //pView.Add(TemporaryEnt);
                            Line line = new Line(toEyeToWorld(X, Y), toEyeToWorld(X, Y));
                            TemporaryEnt = line;
                            //TemporaryEnt.Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B);
                            pView.Add(TemporaryEnt);
                        }
                    }
                    else
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                Line line = (Line)TemporaryEnt;
                                AlignedDimension pDimension = new AlignedDimension();
                                pDimension.SetDatabaseDefaults(database);
                                pDimension.XLine1Point = line.StartPoint;
                                pDimension.XLine2Point = line.EndPoint;
                                pDimension.UsingDefaultTextPosition = true;
                                ObjectId objitem = block.AppendEntity(pDimension);
                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), objitem, null, (Entity)pDimension.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);
                            }
                            trans.Commit();
                        }
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            pView.Erase(TemporaryEnt);
                        }
                        TemporaryEnt = null;
                        InsertLine(false, EntityType.None);
                    }
                    break;
                case EntityType.Img:
                    if (TemporaryEnt != null)
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                ObjectId objitem = block.AppendEntity((Entity)TemporaryEnt.Clone());
                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), objitem, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);
                            }
                            trans.Commit();
                        }
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            pView.Erase(TemporaryEnt);
                        }
                        TemporaryEnt = null;
                        OperModel = Operations.None;
                        EntType = EntityType.None;
                        basepanel.Cursor = Cursors.Default;
                    }
                    break;
                case EntityType.ArcLenDim:
                case EntityType.AnglerotaDim:
                case EntityType.AngleLengthDim:
                case EntityType.AngleArcDim:
                case EntityType.ArcDim:
                case EntityType.Dimension:
                    using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    {
                        selRect = new RectFram(toEyeToWorld(X, Y));
                        pView.Add(selRect);
                    }
                    break;
                default:
                    break;
            }

        }

        private void InsertEntityMove(int X, int Y)
        {
            if (TemporaryEnt != null || selRect != null)
            {
                switch (EntType)
                {
                    case EntityType.Line:
                    case EntityType.LineDim:
                        Line Linet = (Line)TemporaryEnt;
                        Linet.EndPoint = toEyeToWorld(X, Y);
                        break;
                    /*case EntityType.Mline:
                        Mline mine = (Mline)TemporaryEnt;
                        mine.Scale = 0;
                        if (mine.NumberOfVertices <= 1)
                        {
                            mine.AppendSegment(toEyeToWorld(X, Y));
                        }
                        else
                        {
                            Point3d po = mine.VertexAt(mine.NumberOfVertices - 1);
                            mine.RemoveLastSegment(out po);
                            mine.AppendSegment(toEyeToWorld(X, Y));
                        }*/
                    case EntityType.Polyline:
                        Polyline polyline = (Polyline)TemporaryEnt;
                        Point2d currentPoint = new Point2d(toEyeToWorld(X, Y).X, toEyeToWorld(X, Y).Y);

                        if (polyline.NumberOfVertices <= 1)
                        {
                            // 添加第一个点
                            polyline.AddVertexAt(polyline.NumberOfVertices, currentPoint, 0, 0, 0);
                        }
                        else
                        {
                            // 更新最后一个顶点的位置
                            polyline.SetPointAt(polyline.NumberOfVertices - 1, currentPoint);
                        }
                        break;
                    case EntityType.Arc:
                        Arc arc = (Arc)TemporaryEnt;
                        arc.Radius = Function.dis(arc.Center, toEyeToWorld(X, Y));

                        arc.StartAngle = _RollAre * Math.PI / 180;
                        arc.EndAngle = 0.0;


                        break;
                    case EntityType.Circle:
                        Circle cir = (Circle)TemporaryEnt;
                        cir.Radius = Function.dis(cir.Center, toEyeToWorld(X, Y));
                        DrawCircle();
                        break;
                    case EntityType.Point:
                        DBPoint Dbnet = (DBPoint)TemporaryEnt;
                        Dbnet.Position = toEyeToWorld(X, Y);
                        break;
                    case EntityType.Ellipse:
                        //Ellipse ellipses = (Ellipse)TemporaryEnt;
                        //ellipses.set_StartParam(Function.dis(toEyeToWorld(X, Y), ellipses.Center));
                        //ellipses.Set(ellipses.Center, new Vector3d(0, 0, 1), (toEyeToWorld(X, Y) - ellipses.Center), 0.5, 0, 0);
                        break;
                    case EntityType.None:
                        break;
                    case EntityType.Img:
                        RasterImage pImage = (RasterImage)TemporaryEnt;
                        pImage.Orientation = new CoordinateSystem3d(toEyeToWorld(X, Y), new Vector3d(pImage.ImageWidth, 0, 0), new Vector3d(0.0, pImage.ImageHeight, 0));

                        break;
                    case EntityType.leader:
                        if (TemporaryEnt != null)
                        {
                            Leader leaders = (Leader)TemporaryEnt;
                            if (leaders.NumVertices > 1)
                                leaders.SetVertexAt(leaders.NumVertices - 1, toEyeToWorld(X, Y));
                            else
                                leaders.AppendVertex(toEyeToWorld(X, Y));
                        }
                        break;
                    case EntityType.ArcLenDim:
                    case EntityType.AnglerotaDim:
                    case EntityType.AngleLengthDim:
                    case EntityType.AngleArcDim:
                    case EntityType.ArcDim:
                    case EntityType.Dimension:
                        selRect.setValue(toEyeToWorld(X, Y));
                        break;
                    default:
                        break;
                }
            }
            if (helperDevice != null)
                helperDevice.Update();
            helperDevice.Invalidate();
            Invalidate();
        }
        
        private void InsertMouseUp(MouseEventArgs e)
        {
            switch (EntType)
            {
                case EntityType.Line:
                case EntityType.Circle:
                case EntityType.Point:
                    if (e.Button == MouseButtons.Left)
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                ObjectId Id = block.AppendEntity((Entity)TemporaryEnt.Clone());
                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), Id, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);
                            }
                            trans.Commit();
                        }
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            pView.Erase(TemporaryEnt);
                        }
                        TemporaryEnt = null;
                        DragSelected = false;
                    }
                    break;
                case EntityType.Arc:
                    if (e.Button == MouseButtons.Left)
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                ObjectId Id = block.AppendEntity((Entity)TemporaryEnt.Clone());

                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), Id, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);
                            }
                            trans.Commit();
                        }
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            pView.Erase(TemporaryEnt);
                        }
                        TemporaryEnt = null;
                        DragSelected = false;
                    }
                    break;
                /*case EntityType.Mline:
                    if (e.Button == MouseButtons.Right)
                    {

                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                Mline mine = (Mline)TemporaryEnt;
                                mine.Scale = 0;

                                // 闭合 Mline  增加闭合曲线功能
                                if (mine.NumberOfVertices > 1)
                                {
                                    Point3d startPoint = mine.VertexAt(0);
                                    Point3d endPoint = mine.VertexAt(mine.NumberOfVertices - 1);

                                    // 若终点不等于起点，闭合曲线
                                    if (!endPoint.Equals(startPoint))
                                    {
                                        mine.AppendSegment(startPoint); // 添加起点到终点
                                    }
                                }

                                //Point3d po = mine.VertexAt(mine.NumberOfVertices - 1);
                                //mine.RemoveLastSegment(out po);
                                ObjectId Id = block.AppendEntity((Entity)TemporaryEnt.Clone());
                                TemporaryEnt = mine;

                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), Id, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);

                                using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                                {
                                    pView.Erase(TemporaryEnt);
                                    pView.Erase(mine);
                                }
                            }
                            trans.Commit();
                        }
                        TemporaryEnt = null;
                        DragSelected = false;
                    }
                    break;*/
                case EntityType.Polyline:
                    //if (e.Button == MouseButtons.Right)
                    //{
                    //    using (var trans = database.TransactionManager.StartTransaction())
                    //    {
                    //        using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                    //        {
                    //            Polyline polyline = (Polyline)TemporaryEnt;

                    //            // 闭合 Polyline，增加闭合曲线功能
                    //            if (polyline.NumberOfVertices > 1)
                    //            {
                    //                Point2d startPoint = polyline.GetPoint2dAt(0);
                    //                Point2d endPoint = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);

                    //                // 若终点不等于起点，则闭合曲线
                    //                if (!endPoint.Equals(startPoint))
                    //                {
                    //                    polyline.AddVertexAt(polyline.NumberOfVertices, startPoint, 0, 0, 0); // 添加起点到终点
                    //                    polyline.Closed = true; // 设置 Polyline 为闭合
                    //                }
                    //            }

                    //            //添加包围盒
                    //            mergedPolylines.Add(polyline);

                    //            ObjectId Id = block.AppendEntity((Entity)TemporaryEnt.Clone());
                    //            TemporaryEnt = polyline;

                    //            ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), Id, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                    //            Invoker.SetCommad(entCommand);

                    //            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                    //            {
                    //                pView.Erase(TemporaryEnt);
                    //            }
                    //        }
                    //        trans.Commit();
                    //    }
                    //    TemporaryEnt = null;
                    //    DragSelected = false;
                    //}










                    if (e.Button == MouseButtons.Right)
                    {
                        using (var trans = database.TransactionManager.StartTransaction())
                        {
                            using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false, false))
                            {
                                Polyline polyline = (Polyline)TemporaryEnt;

                                // 闭合 Polyline，增加闭合曲线功能
                                if (polyline.NumberOfVertices > 1)
                                {
                                    Point2d startPoint = polyline.GetPoint2dAt(0);
                                    Point2d endPoint = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);

                                    // 若终点不等于起点，则闭合曲线
                                    if (!endPoint.Equals(startPoint))
                                    {
                                        polyline.AddVertexAt(polyline.NumberOfVertices, startPoint, 0, 0, 0); // 添加起点到终点
                                        polyline.Closed = true; // 设置 Polyline 为闭合
                                    }
                                }

                                // 创建一个新的PolylineWithIndex对象，并为其分配序号
                                PolylineWithIndex polylineWithIndex = new PolylineWithIndex
                                {
                                    Polyline = polyline,
                                    Index = mergedPolylinesWithIndex.Count + 1  // 按顺序分配序号
                                };

                                // 检查与当前已存在包围盒的相交或包含关系
                                List<PolylineWithIndex> toRemove = new List<PolylineWithIndex>();  // 存储需要删除的包围盒

                                foreach (PolylineWithIndex existingPolyline in mergedPolylinesWithIndex)
                                {
                                    // 假设 existingPolyline.Polyline 和 polylineWithIndex.Polyline 是 Polyline 类型
                                    List<Curve> curves = new List<Curve> { existingPolyline.Polyline, polylineWithIndex.Polyline };

                                    // 计算这两个 Polyline 的包围盒
                                    Extents3d existingBox = CalculateBoundingBox2(new List<Curve> { existingPolyline.Polyline });
                                    Extents3d newBox = CalculateBoundingBox2(new List<Curve> { polylineWithIndex.Polyline });



                                    // 判断当前 polyline 是否与已存在的包围盒相交或包含
                                    if (Overlaps(existingBox, newBox) || Contains(existingBox, newBox))
                                    {
                                        // 如果新包围盒包含或相交于现有包围盒，将其加入删除列表
                                        toRemove.Add(existingPolyline);
                                    }
                                }

                                // 删除相交或包含的包围盒
                                foreach (PolylineWithIndex polylineToRemove in toRemove)
                                {
                                    mergedPolylinesWithIndex.Remove(polylineToRemove);
                                }






                                // 将此 PolylineWithIndex 存储到全局变量
                                mergedPolylinesWithIndex.Add(polylineWithIndex);

                                //添加包围盒
                                //mergedPolylines.Add(polyline);

                                ObjectId Id = block.AppendEntity((Entity)TemporaryEnt.Clone());
                                TemporaryEnt = polyline;

                                ReceiverEntity entCommand = new ReceiverEntity(new ObjectId(), Id, null, (Entity)TemporaryEnt.Clone(), ref database, ref helperDevice);
                                Invoker.SetCommad(entCommand);

                                using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                                {
                                    pView.Erase(TemporaryEnt);
                                }
                            }
                            trans.Commit();
                        }
                        TemporaryEnt = null;
                        DragSelected = false;
                    }
















                    break;
                case EntityType.None:
                    break;
                case EntityType.Img:
                    break;
                case EntityType.LineDim:
                    break;
                default:
                    if (selRect != null)
                    {
                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                        {
                            ObjectIdCollection objection = new ObjectIdCollection();
                            pView.Select(new Point2dCollection(new Point2d[] { new Point2d(MouseDownPt.X, MouseDownPt.Y), new Point2d(e.X, e.Y) }),
                              new SRnew(objection, database.CurrentSpaceId), MouseDownPt.X < e.X ? Teigha.GraphicsSystem.SelectionMode.Window : Teigha.GraphicsSystem.SelectionMode.Crossing);
                            pView.Erase(selRect);
                            selRect = null;
                            using (var trans = database.TransactionManager.StartTransaction())
                            {
                                List<Entity> entold = new List<Entity>();
                                List<Entity> newold = new List<Entity>();
                                ObjectIdCollection oldids = new ObjectIdCollection();
                                ObjectIdCollection newids = new ObjectIdCollection();

                                using (BlockTableRecord block = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite, false))
                                    foreach (ObjectId item in objection)
                                    {
                                        using (Entity ent = (Entity)item.GetObject(OpenMode.ForWrite, false))
                                        {
                                            #region ArcLenDim
                                            if (EntType == EntityType.ArcLenDim)
                                            {
                                                if (ent.GetType().Name == "Arc")
                                                    using (Arc pArc = (Arc)ent)
                                                    {
                                                        using (ArcDimension pDimension = new ArcDimension(pArc.Center, pArc.StartPoint, pArc.EndPoint, pArc.Center + new Vector3d(pArc.Radius + 0.45, 0.0, 0.0), "", ObjectId.Null))
                                                        {
                                                            pDimension.SetDatabaseDefaults(database);
                                                            ObjectId id = block.AppendEntity(pDimension);
                                                            pDimension.ArcSymbolType = 1;
                                                            pDimension.UsingDefaultTextPosition = true;

                                                            oldids.Add(ObjectId.Null);
                                                            newids.Add(id);
                                                            newold.Add((Entity)pDimension.Clone());
                                                        }
                                                    }
                                            }
                                            #endregion

                                            #region AnglerotaDim
                                            if (EntType == EntityType.AnglerotaDim)
                                            {
                                                if (ent.GetType().Name == "Line")
                                                    using (Line pLine = (Line)ent)
                                                    {
                                                        using (RotatedDimension pDimension = new RotatedDimension(0, pLine.StartPoint,
                                                        pLine.EndPoint, new Point3d((pLine.StartPoint.X + pLine.EndPoint.X) / 2, pLine.StartPoint.Y, 0), String.Format("倾斜注释:" + Function.Angle(pLine.StartPoint, new Point3d(pLine.StartPoint.X + 10, pLine.StartPoint.Y, 0), pLine.EndPoint) + "°"), ObjectId.Null))
                                                        {
                                                            pDimension.SetDatabaseDefaults(database);
                                                            ObjectId id = block.AppendEntity(pDimension);
                                                            pDimension.UsingDefaultTextPosition = true;

                                                            oldids.Add(ObjectId.Null);
                                                            newids.Add(id);
                                                            newold.Add((Entity)pDimension.Clone());
                                                        }
                                                    }
                                            }
                                            #endregion

                                            #region AngleArcDim
                                            if (EntType == EntityType.AngleArcDim)
                                            {
                                                if (ent.GetType().Name == "Arc")
                                                    using (Arc pArc = (Arc)ent)
                                                    {
                                                        using (Point3AngularDimension pDimension = new Point3AngularDimension())
                                                        {
                                                            pDimension.SetDatabaseDefaults(database);
                                                            ObjectId id = block.AppendEntity(pDimension);
                                                            pDimension.CenterPoint = pArc.Center;
                                                            pDimension.ArcPoint = pArc.Center + new Vector3d(pArc.Radius + 0.45, 0.0, 0.0);
                                                            pDimension.XLine1Point = pArc.StartPoint;
                                                            pDimension.XLine2Point = pArc.EndPoint;
                                                            pDimension.UsingDefaultTextPosition = true;

                                                            oldids.Add(ObjectId.Null);
                                                            newids.Add(id);
                                                            newold.Add((Entity)pDimension.Clone());
                                                        }
                                                    }
                                            }
                                            #endregion

                                            #region ArcDim
                                            if (EntType == EntityType.ArcDim)
                                            {
                                                if (ent.GetType().Name == "Arc")
                                                    using (Arc pArc = (Arc)ent)
                                                    {
                                                        using (RadialDimensionLarge pDimension = new RadialDimensionLarge())
                                                        {
                                                            ObjectId id = block.AppendEntity(pDimension);

                                                            pDimension.SetDatabaseDefaults(database);

                                                            Point3d centerPoint, chordPoint, overrideCenter, jogPoint, textPosition;

                                                            centerPoint = pArc.Center;
                                                            chordPoint = centerPoint + new Vector3d(pArc.Radius, 0.0, 0.0).RotateBy(0.5 * (pArc.StartAngle + pArc.EndAngle), Vector3d.ZAxis);

                                                            overrideCenter = centerPoint + new Vector3d(pArc.Radius * 3.0 / 8.0, 0.0, 0.0);

                                                            jogPoint = overrideCenter + 0.5 * (chordPoint - overrideCenter);

                                                            textPosition = centerPoint + 0.7 * (chordPoint - centerPoint);

                                                            double jogAngle = 45 * Math.PI / 180; ;

                                                            pDimension.Center = centerPoint;
                                                            pDimension.ChordPoint = pArc.Center + new Vector3d(pArc.Radius, 0.0, 0.0).RotateBy(0.5 * (pArc.StartAngle + pArc.EndAngle), Vector3d.ZAxis);
                                                            pDimension.OverrideCenter = overrideCenter;
                                                            pDimension.JogPoint = jogPoint;
                                                            pDimension.TextPosition = textPosition;
                                                            pDimension.JogAngle = jogAngle;
                                                            pDimension.UsingDefaultTextPosition = true;

                                                            oldids.Add(ObjectId.Null);
                                                            newids.Add(id);
                                                            newold.Add((Entity)pDimension.Clone());
                                                        }
                                                    }
                                            }
                                            #endregion

                                            #region Dimension
                                            if (EntType == EntityType.Dimension)
                                            {
                                                if (ent.GetType().Name == "Arc")
                                                {
                                                    Arc pArc = (Arc)ent;
                                                    using (ArcDimension pDimension = new ArcDimension(pArc.Center, pArc.StartPoint, pArc.EndPoint, pArc.Center + new Vector3d(pArc.Radius + 0.45, 0.0, 0.0), "", ObjectId.Null))
                                                    {
                                                        pDimension.SetDatabaseDefaults(database);
                                                        ObjectId id = block.AppendEntity(pDimension);
                                                        pDimension.UsingDefaultTextPosition = true;
                                                        pDimension.ArcSymbolType = 1;

                                                        oldids.Add(ObjectId.Null);
                                                        newids.Add(id);
                                                        newold.Add((Entity)pDimension.Clone());
                                                    }
                                                }
                                                else if (ent.GetType().Name == "Line")
                                                {
                                                    Line line = (Line)ent;
                                                    AlignedDimension pDimension = new AlignedDimension();
                                                    pDimension.SetDatabaseDefaults(database);
                                                    pDimension.XLine1Point = line.StartPoint;
                                                    pDimension.XLine2Point = line.EndPoint;
                                                    pDimension.UsingDefaultTextPosition = true;
                                                    ObjectId id = block.AppendEntity(pDimension);

                                                    oldids.Add(ObjectId.Null);
                                                    newids.Add(id);
                                                    newold.Add((Entity)pDimension.Clone());
                                                }
                                                else if (ent.GetType().Name == "Polyline")
                                                {
                                                    Teigha.DatabaseServices.Polyline line = (Teigha.DatabaseServices.Polyline)ent;
                                                    for (int i = 0; i < line.NumberOfVertices; i++)
                                                    {
                                                        switch (line.GetSegmentType(i))
                                                        {
                                                            case SegmentType.Empty:
                                                                break;
                                                            case SegmentType.Point:
                                                                break;
                                                            case SegmentType.Arc:
                                                                CircularArc3d pArc = line.GetArcSegmentAt(i);
                                                                using (ArcDimension pDimension = new ArcDimension(pArc.Center, new Point3d(pArc.StartPoint.X, pArc.StartPoint.Y, 0), new Point3d(pArc.EndPoint.X, pArc.EndPoint.Y, 0), pArc.Center + new Vector3d(pArc.Radius + 0.45, 0.0, 0.0), "", ObjectId.Null))
                                                                {
                                                                    pDimension.SetDatabaseDefaults(database);
                                                                    ObjectId idw = block.AppendEntity(pDimension);
                                                                    pDimension.ArcSymbolType = 1;
                                                                    pDimension.UsingDefaultTextPosition = true;

                                                                    oldids.Add(ObjectId.Null);
                                                                    newids.Add(idw);
                                                                    newold.Add((Entity)pDimension.Clone());
                                                                }
                                                                break;
                                                            case SegmentType.Coincident:
                                                                break;
                                                            case SegmentType.Line:
                                                                LineSegment3d lines = line.GetLineSegmentAt(i);
                                                                AlignedDimension pDimension2 = new AlignedDimension();
                                                                pDimension2.SetDatabaseDefaults(database);
                                                                pDimension2.XLine1Point = lines.StartPoint;
                                                                pDimension2.XLine2Point = lines.EndPoint;
                                                                pDimension2.UsingDefaultTextPosition = true;
                                                                ObjectId id = block.AppendEntity(pDimension2);

                                                                oldids.Add(ObjectId.Null);
                                                                newids.Add(id);
                                                                newold.Add((Entity)pDimension2.Clone());
                                                                break;
                                                            default:
                                                                break;
                                                        }
                                                    }
                                                }

                                            }
                                            #endregion
                                        }
                                    }

                                if (objection.Count > 0)
                                {
                                    ReceiverSelection receSelection = new ReceiverSelection(oldids, newids, entold, newold, ref database, ref helperDevice);
                                    Invoker.SetCommad(receSelection);
                                }
                                trans.Commit();
                            }
                        }
                        DragSelected = false;
                        InsertLine(false, EntityType.None);
                    }
                    break;
            }

            if (helperDevice != null)
                helperDevice.Update();
            helperDevice.Invalidate();
            Invalidate();
        }

        /// <summary>
        /// 启动拖拽界面
        /// </summary>
        public void SetmouseTypeOk(bool bol = true)
        {
            if (bol)
            {
                basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.拖拽32白色.GetHicon());
                OperModel = Operations.Drag;
            }
            else
            {
                basepanel.Cursor = Cursors.Default;
                DragSelected = false;
                OperModel = Operations.None;
            }
        }

        /// <summary>
        /// 插入dwf
        /// </summary>
        public void InsertDwfBy()
        {
            OpenFileDialog saveDialog = new OpenFileDialog();
            saveDialog.Filter = String.Format("DWF (Desgin Web Format) (*.dwf)|*.dwf");
            if (DialogResult.OK == saveDialog.ShowDialog())
            {
                SystemObjects.DynamicLinker.LoadApp("TD_Dwf7Import", false, false);
                using (DWFImport dwfimp = new DWFImport())
                {
                    using (Dictionary props = dwfimp.Properties)
                    {
                        if (props.Contains("Database"))
                            props.AtPut("Database", database);
                        if (props.Contains("DwfPath")) // Check if property is supported
                            props.AtPut("DwfPath", new RxVariant(saveDialog.FileName));
                        if (props.Contains("Password")) // Check if property is supported
                            props.AtPut("Password", new RxVariant(String.Format("")));
                        if (props.Contains("PaperWidth")) // Check if property is supported
                            props.AtPut("PaperWidth", new RxVariant(297.0));
                        if (props.Contains("PaperHeight")) // Check if property is supported
                            props.AtPut("PaperHeight", new RxVariant(210.0));
                        if (props.Contains("PreserveColorIndices")) // Check if property is supported
                            props.AtPut("PreserveColorIndices", new RxVariant(true));
                        if (props.Contains("LayoutNumber")) // Check if property is supported
                            props.AtPut("LayoutNumber", new RxVariant(-1));
                        if (props.Contains("ImportW3d")) // Check if property is supported
                            props.AtPut("ImportW3d", new RxVariant(true));
                    }
                    dwfimp.Import();
                }
            }
        }

        /// <summary>
        /// 插入图片
        /// </summary>
        public void InsertImages()
        {
            OpenFileDialog saveDialog = new OpenFileDialog();
            saveDialog.Filter = String.Format("img (*.jpg)|*.jpg");
            if (DialogResult.OK == saveDialog.ShowDialog())
            {
                string path = Path.GetDirectoryName(saveDialog.FileName);
                string Imgpath = Path.Combine(Path.GetDirectoryName(DwgPath), Path.GetFileName(saveDialog.FileName));
                if (Path.GetDirectoryName(DwgPath) != path)
                    File.Copy(saveDialog.FileName, Imgpath, true);

                System.Drawing.Image imgs = System.Drawing.Image.FromFile(saveDialog.FileName);
                int w = imgs.Width;
                int h = imgs.Height;
                imgs.Dispose();
                using (var trans = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        using (BlockTableRecord bBTR = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite))
                        {
                            ObjectId imageDictId = RasterImageDef.CreateImageDictionary(bBTR.Database);
                            using (DBDictionary pImageDict = (DBDictionary)imageDictId.GetObject(OpenMode.ForWrite))
                            {

                                /**********************************************************************/
                                /* Create an ImageDef object                                          */
                                /**********************************************************************/
                                using (RasterImageDef pImageDef = new RasterImageDef())
                                {
                                    ObjectId imageDefId = pImageDict.SetAt("timg", pImageDef);

                                    /**********************************************************************/
                                    /* Set some parameters                                                */
                                    /**********************************************************************/
                                    pImageDef.SourceFileName = String.Format("timg.jpg");

                                    using (GIRasterImageDesc img = new GIRasterImageDesc((uint)w, (uint)h, Units.Inch))
                                    {
                                        pImageDef.SetImage(img.UnmanagedObject, (IntPtr)0, true);

                                        /**********************************************************************/
                                        /* Create an Image object                                             */
                                        /**********************************************************************/
                                        //using (RasterImage pImage = new RasterImage())
                                        //{
                                        //    pImage.SetDatabaseDefaults(bBTR.Database);
                                        //    bBTR.AppendEntity(pImage);
                                        //    pImage.ShowImage = true;
                                        //    /**********************************************************************/
                                        //    /* Set some parameters                                                */
                                        //    /**********************************************************************/
                                        //    pImage.ImageDefId = imageDefId;
                                        //    pImage.Orientation = new CoordinateSystem3d(toEyeToWorld(MouseDownPt.X, MouseDownPt.Y), new Vector3d(w, 0, 0), new Vector3d(0.0, h, 0));
                                        //    pImage.DisplayOptions = ImageDisplayOptions.Show | ImageDisplayOptions.ShowUnaligned;

                                        //}

                                        using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                                        {
                                            RasterImage pImage = new RasterImage();
                                            pImage.SetDatabaseDefaults(bBTR.Database);
                                            pView.Add(pImage);
                                            pImage.ShowImage = true;
                                            /**********************************************************************/
                                            /* Set some parameters                                                */
                                            /**********************************************************************/
                                            pImage.ImageDefId = imageDefId;
                                            pImage.Orientation = new CoordinateSystem3d(toEyeToWorld(MouseDownPt.X, MouseDownPt.Y), new Vector3d(w, 0, 0), new Vector3d(0.0, h, 0));
                                            pImage.DisplayOptions = ImageDisplayOptions.Show | ImageDisplayOptions.ShowUnaligned;

                                            TemporaryEnt = pImage;
                                        }
                                    }

                                }
                            }
                        }

                        OperModel = Operations.InsertEntity;
                        EntType = EntityType.Img;
                        basepanel.Cursor = new System.Windows.Forms.Cursor(Properties.Resources.十字光标.GetHicon());
                        trans.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        trans.Abort();
                        OperModel = Operations.None;
                        EntType = EntityType.None;
                        basepanel.Cursor = Cursors.Default;
                        KryptonMessageBox.Show("插入图片失败！错误原因：" + ex.Message);
                    }
                }
            }
            else
            {
                OperModel = Operations.None;
                EntType = EntityType.None;
                basepanel.Cursor = Cursors.Default;
            }
            if (helperDevice != null)
                helperDevice.Update();
            helperDevice.Invalidate();
            Invalidate();
        }

        private void 打开文件所在位置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(System.IO.Path.GetDirectoryName(DwgPath)))
            {
                KryptonMessageBox.Show("项目路径【" + System.IO.Path.GetDirectoryName(DwgPath) + "】不存在！");
                return;
            }
            System.Diagnostics.Process open = new System.Diagnostics.Process();
            open.StartInfo.FileName = "explorer";
            open.StartInfo.Arguments = @"/select," + DwgPath;
            open.Start();
        }

        /// <summary>
        /// 添加实体备注
        /// </summary>
        public void AddEntity_Xdata(string StrText)
        {
            using (var trans = database.TransactionManager.StartTransaction())
            {
                using (RegAppTable appTbl = trans.GetObject(database.RegAppTableId, OpenMode.ForWrite) as RegAppTable)
                    if (!appTbl.Has(Application.ProductName))
                    {
                        RegAppTableRecord appTblRcd = new RegAppTableRecord();
                        appTblRcd.Name = Application.ProductName;
                        appTbl.Add(appTblRcd);
                        trans.AddNewlyCreatedDBObject(appTblRcd, true);
                    }

                if (selected.Count > 0)
                {
                    foreach (ObjectId item in selected)
                    {
                        using (Entity ent = (Entity)item.GetObject(OpenMode.ForWrite, false))
                        {
                            ResultBuffer resBuf = ent.XData;
                            if (resBuf != null)
                            {
                                resBuf.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, StrText));
                                ent.XData = resBuf;
                            }
                            else
                            {
                                ResultBuffer resBufNEw = new ResultBuffer();
                                resBufNEw.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, Application.ProductName));
                                resBufNEw.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, StrText));
                                ent.XData = resBufNEw;
                            }
                        }
                    }
                }
                trans.Commit();
            }
        }

        #endregion

        //接地图
        public class CustomEntityInfo
        {
            public string Type { get; set; }
            public string Content { get; set; }
            public Point3d Coordinates { get; set; }
        }
        public void ClassifyEntities(object sender, EventArgs e)
        {
            // 初始化分类列表
            List<CustomEntityInfo> groundPoints = new List<CustomEntityInfo>();
            List<CustomEntityInfo> wireHarnesses = new List<CustomEntityInfo>();
            List<CustomEntityInfo> loads = new List<CustomEntityInfo>();

            if (database == null)
            {
                Console.WriteLine("Database is null, cannot classify entities.");
                return;
            }

            using (Transaction tr = database.TransactionManager.StartTransaction())
            {
                if (database.BlockTableId.IsNull)
                {
                    Console.WriteLine("BlockTableId is null, cannot proceed.");
                    return;
                }

                BlockTable blockTable = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (blockTable == null)
                {
                    Console.WriteLine("BlockTable is null, cannot proceed.");
                    return;
                }

                BlockTableRecord modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (modelSpace == null)
                {
                    Console.WriteLine("ModelSpace is null, cannot proceed.");
                    return;
                }

                // 遍历模型空间中的所有实体
                foreach (ObjectId objId in modelSpace)
                {
                    Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        string type = entity.GetType().Name;
                        string content = string.Empty;
                        Point3d coordinates = new Point3d();

                        // 根据实体类型提取字符内容和坐标
                        switch (entity)
                        {
                            case DBText dbText:
                                content = dbText.TextString;
                                coordinates = dbText.Position;
                                break;

                            case MText mText:
                                content = mText.Text;
                                coordinates = mText.Location;
                                break;

                            case Line line:
                                content = string.Empty; // 线条没有文本内容
                                coordinates = line.StartPoint;
                                break;

                            default:
                                break;
                        }

                        // 根据内容分类实体
                        if (!string.IsNullOrEmpty(content))
                        {
                            if (content.Contains("GND"))
                            {
                                groundPoints.Add(new CustomEntityInfo
                                {
                                    Type = type,
                                    Content = content,
                                    Coordinates = coordinates
                                });
                            }
                            else if (content.StartsWith("Z") && content.Length > 0 && char.IsDigit(content[content.Length - 1]) && content.Count(c => c == ' ') == 2) // 线束信息
                            {
                                wireHarnesses.Add(new CustomEntityInfo
                                {
                                    Type = type,
                                    Content = content,
                                    Coordinates = coordinates
                                });
                            }
                            else
                            {
                                loads.Add(new CustomEntityInfo
                                {
                                    Type = type,
                                    Content = content,
                                    Coordinates = coordinates
                                });
                            }
                        }
                    }
                }

                // 提交事务
                tr.Commit();
            }
            MatchEntities(groundPoints, wireHarnesses, loads);
            // 输出到文本文件
            OutputToTextFile("groundPoints.txt", groundPoints);
            OutputToTextFile("wireHarnesses.txt", wireHarnesses);
            OutputToTextFile("loads.txt", loads);
        }
        // 将实体列表输出到文本文件的函数
        private void OutputToTextFile(string fileName, List<CustomEntityInfo> entities)
        {
            // 删除现有的文件
            File.Delete(fileName);

            // 遍历实体并将其信息写入文件
            foreach (var entity in entities)
            {
                // 将内容和坐标输出到文件
                File.AppendAllText(fileName, $"Type: {entity.Type}, Content: {entity.Content}, Coordinates: {entity.Coordinates}\n");
            }
        }

        public void MatchEntities(List<CustomEntityInfo> groundPoints, List<CustomEntityInfo> wireHarnesses, List<CustomEntityInfo> loads)
        {
            // 用于存储接地点与其匹配的线束及负载
            var groundToHarnesses = new Dictionary<CustomEntityInfo, List<Tuple<CustomEntityInfo, CustomEntityInfo>>>();

            // 遍历每根线束
            foreach (var harness in wireHarnesses)
            {
                // 找到符合条件的接地点（要求线束在接地点的右侧且在上方）
                var nearestGround = groundPoints
                    .Where(g => harness.Coordinates.X > g.Coordinates.X // 线束在接地点的右侧
                                && harness.Coordinates.Y > g.Coordinates.Y) // 线束在接地点的上方
                    .OrderBy(g => Math.Sqrt(Math.Pow(harness.Coordinates.X - g.Coordinates.X, 2)
                                           + Math.Pow(harness.Coordinates.Y - g.Coordinates.Y, 2))) // 按距离最近排序
                    .FirstOrDefault();

                if (nearestGround != null)
                {
                    // 找到与线束匹配的负载（要求线束在负载左侧，Y方向距离不超过4单位）
                    var nearestLoad = loads
                        .Where(l => harness.Coordinates.X < l.Coordinates.X // 线束在负载左侧
                                    && Math.Abs(harness.Coordinates.Y - l.Coordinates.Y) <= 4) // Y方向距离小于等于4
                        .OrderBy(l => Math.Abs(harness.Coordinates.X - l.Coordinates.X)) // 按 X 坐标距离最近排序
                        .FirstOrDefault();

                    // 将线束和负载信息记录到对应的接地点
                    if (!groundToHarnesses.ContainsKey(nearestGround))
                    {
                        groundToHarnesses[nearestGround] = new List<Tuple<CustomEntityInfo, CustomEntityInfo>>();
                    }

                    // 使用 new Tuple<>() 方式创建元组
                    groundToHarnesses[nearestGround].Add(new Tuple<CustomEntityInfo, CustomEntityInfo>(harness, nearestLoad));
                }
            }

            // 输出匹配结果到文件
            List<string> results = new List<string>();
            foreach (var ground in groundToHarnesses)
            {
                // 输出接地点信息
                results.Add($"接地点: {ground.Key.Content}");

                // 输出每根线束及其负载
                foreach (var harnessLoadPair in ground.Value)
                {
                    results.Add($"  线束信息: {harnessLoadPair.Item1.Content}");
                    if (harnessLoadPair.Item2 != null)
                    {
                        results.Add($"    负载: {harnessLoadPair.Item2.Content}");
                    }
                }

                results.Add(""); // 添加空行以便区分不同接地点
            }

            File.WriteAllLines("MatchResults.txt", results);
        }
        




        
        //DFS键盘输入缓冲区窗口
        private string ShowInputDialog(string text, string caption, string defaultValue)
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 150,
                Text = caption
            };

            Label textLabel = new Label() { Left = 10, Top = 20, Text = text, Width = 260 };
            TextBox inputBox = new TextBox() { Left = 10, Top = 50, Width = 260, Text = defaultValue };
            Button confirmation = new Button() { Text = "确认", Left = 200, Width = 70, Top = 80 };
            confirmation.DialogResult = DialogResult.OK;

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : string.Empty;
        }

        // 全局变量，保存已删除的实体
        private List<Entity> deletedEntities = new List<Entity>();

        //BFS
        public void DeleteLine_BFS(object sender, EventArgs e)
        {
            // 检查是否选中了有效的 Polyline
            if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
            {
                MessageBox.Show("请先选择一条 Polyline 作为中轴线。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 获取用户输入的缓冲区宽度
            string input = ShowInputDialog("请输入缓冲区宽度：", "缓冲区宽度", "5.0");
            if (!double.TryParse(input, out double bufferWidth) || bufferWidth <= 0)
            {
                MessageBox.Show("无效的宽度值，请输入正数。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite);

                    // 加载选中的 Polyline
                    Polyline centralPolyline = tr.GetObject(GlobalEntityData.SelectedEntityId, OpenMode.ForRead) as Polyline;
                    startpoint = centralPolyline.GetPoint3dAt(0);
                    if (centralPolyline == null)
                    {
                        MessageBox.Show("选中的实体不是有效的 Polyline。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    List<Poly> Polys = new List<Poly>();

                    foreach (ObjectId oid in btr)
                    {
                        DBObject dbobj = tr.GetObject(oid, OpenMode.ForWrite);
                        if (dbobj is Entity)
                        {

                            Entity entity = dbobj as Entity;
                            string enttype = entity.GetRXClass().Name;
                            entity.Unhighlight();

                            if (enttype == "AcDbPolyline")
                            {
                                Polyline acPolyline = entity as Polyline;//筛选多段线段                                                       
                                Poly pl;
                                pl.line = acPolyline;
                                pl.visit = false;
                                Polys.Add(pl);
                            }
                        }
                    }

                    List<tuple> tuple = new List<tuple>();//用于表示一对点，可能是用于路径的起点和终点。
                    Queue<Point3d> findpoint = new Queue<Point3d>();//队列来执行广度优先搜索或路径查找，初始化时加入起始点 startpoint
                    findpoint.Enqueue(startpoint);
                    while (findpoint.Count != 0)
                    {
                        //从队列中取出当前点作为 midpoint，并将其作为 midtuple 的起点。
                        Point3d midpoint = findpoint.Dequeue();
                        tuple midtuple;
                        midtuple.start = midpoint;
                        int j = 0;//用于限制每次查找的最大路径数，防止无限循环。

                        for (int ij = 0; ij < Polys.Count; ij++)
                        {
                            Poly i = Polys[ij];//获取当前检查的多段线

                            // 如果当前多段线未被访问过，且起点接近当前中点，则继续查找
                            if (!i.visit)
                            {
                                // 判断中点是否与多段线的起点接近
                                if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(0).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(0).Y) < 0.1f)
                                {
                                    // 如果接近，则将多段线的终点加入到查找队列
                                    findpoint.Enqueue(i.line.GetPoint3dAt(1));

                                    // 设置当前路径的终点为多段线的终点
                                    midtuple.end = i.line.GetPoint3dAt(1);

                                    // 将当前路径的起点和终点添加到tuple列表
                                    tuple.Add(midtuple);

                                    // 标记当前多段线为已访问
                                    i.visit = true;

                                    // 更新Polys列表中该多段线的状态
                                    Polys[ij] = i;

                                    // 增加路径查找的计数器
                                    j++;

                                    // 如果查找的路径次数达到6次，则停止进一步查找
                                    if (j >= 6)
                                    {
                                        break;
                                    }
                                }

                                // 如果当前中点接近多段线的终点，则反向查找
                                else if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(1).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(1).Y) < 0.1f)
                                {
                                    // 将多段线的起点加入到查找队列
                                    findpoint.Enqueue(i.line.GetPoint3dAt(0));
                                    midtuple.end = i.line.GetPoint3dAt(0);
                                    tuple.Add(midtuple);

                                    i.visit = true;

                                    Polys[ij] = i;
                                    continue;

                                }
                            }
                        }
                    }

                    // 使用LINQ提取visit属性为true的Polyline对象
                    List<Polyline> visitedPolylines = Polys
                        .Where(poly => poly.visit)  // 过滤visit为true的
                        .Select(poly => poly.line)  // 提取对应的Polyline对象
                        .ToList();


                    // 使用 DFS 查找所有直接或间接相连的曲线
                    //HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();


                    //DFS(centralPolyline.ObjectId, connectedPolylines, tr);

                    HashSet<ObjectId> curvesToHide = new HashSet<ObjectId>();

                    // 1. 初始化四叉树
                    double xMin = Math.Min(Global.x1, Global.x2);
                    double yMin = Math.Min(Global.y1, Global.y2);
                    double xMax = Math.Max(Global.x1, Global.x2);
                    double yMax = Math.Max(Global.y1, Global.y2);
                    QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 1);

                    // 2. 将模型空间中的曲线添加到四叉树
                    foreach (ObjectId objId in btr)
                    {
                        if (curvesToHide.Contains(objId)) continue;

                        if (objId.IsValid && !objId.IsErased && objId.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Entity))))
                        {
                            Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (entity != null)
                            {

                                if (entity is Curve curve)
                                {
                                    // 将 Polyline 添加到四叉树
                                    quadTree.AddNode(curve.GeometricExtents.MinPoint, curve);
                                }

                            }
                        }
                    }

                    // 3. 为每个相连的 Polyline 生成缓冲区并查找相交曲线
                    foreach (Polyline polyline in visitedPolylines)
                    {
                        if (polyline == null || polyline.NumberOfVertices < 2)
                        {
                            MessageBox.Show("选中的 Polyline 顶点不足，无法进行分段处理。");
                            continue;
                        }

                        // 将 Polyline 分段
                        List<Polyline> straightSegments = BreakPolylineIntoSegmentPolylines(polyline);

                        foreach (Polyline segment in straightSegments)
                        {
                            // 创建缓冲区
                            Polyline offsetBuffer = CreateManualOffsetPolyline(segment, bufferWidth);

                            if (offsetBuffer != null)
                            {
                                // 计算缓冲区的矩形范围
                                Extents3d bounds = offsetBuffer.GeometricExtents;
                                Rect queryBox = new Rect(bounds.MinPoint.X, bounds.MinPoint.Y, bounds.MaxPoint.X - bounds.MinPoint.X, bounds.MaxPoint.Y - bounds.MinPoint.Y);

                                // 4. 查询与缓冲区相交的曲线
                                List<Curve> candidates = new List<Curve>();
                                quadTree.GetNode(queryBox, ref candidates);

                                // 5. 判断相交
                                foreach (Curve candidate in candidates)
                                {
                                    // 确保 candidate 是 Polyline 类型
                                    //if (candidate is Polyline modelPolyline)
                                    {
                                        // 判断是否相交
                                        //if (IsPolylineInsideOrIntersectingRectangle(modelPolyline, offsetBuffer))
                                        {
                                            curvesToHide.Add(candidate.ObjectId);
                                        }
                                    }

                                }
                            }
                            else
                            {
                                // 处理 offsetBuffer 为 null 的情况
                                Console.WriteLine("offsetBuffer is null");
                            }
                        }
                    }

                    // 隐藏所有找到的相交曲线
                    foreach (ObjectId curveId in curvesToHide)
                    {
                        Entity ent = tr.GetObject(curveId, OpenMode.ForWrite) as Entity;
                        if (ent != null)
                        {
                            // 复制实体副本并添加到列表中
                            Entity entCopy = ent.Clone() as Entity;
                            if (entCopy != null)
                            {
                                deletedEntities.Add(entCopy);
                            }

                            //deletedEntities.Add(ent);  // 保存已删除的实体
                            ent.Erase(); // 删除曲线
                        }
                    }

                    tr.Commit();
                    MessageBox.Show("缓冲区生成并隐藏相交的曲线完成！");
                }
            }
            catch (System.Exception ex)
            {

                MessageBox.Show($"发生错误：{ex.Message}\n堆栈信息：{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }

        // 将带有拐点的 Polyline 拆分为多个直线段组成的 Polyline
        private List<Polyline> BreakPolylineIntoSegmentPolylines(Polyline polyline)
        {
            List<Polyline> segments = new List<Polyline>();
            int numVertices = polyline.NumberOfVertices;

            for (int i = 0; i < numVertices - 1; i++)
            {
                Point3d startPoint = polyline.GetPoint3dAt(i);
                Point3d endPoint = polyline.GetPoint3dAt(i + 1);

                Polyline segment = new Polyline();
                segment.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
                segment.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
                segment.Closed = false;

                segments.Add(segment);
            }
            return segments;
        }

        // 创建缓冲区 Polyline，生成矩形偏移缓冲区-----这里解决了第一幅图的报错
        private Polyline CreateManualOffsetPolyline(Polyline lineSegment, double bufferWidth)
        {
            if (lineSegment.NumberOfVertices != 2) return null;

            Point2d pt1 = lineSegment.GetPoint2dAt(0);
            Point2d pt2 = lineSegment.GetPoint2dAt(1);
            Vector2d direction = pt2 - pt1;

            // 如果方向向量长度接近零，跳过生成缓冲区
            if (direction.Length < Tolerance.Global.EqualPoint * 10) // 使用更大的容差
            {
                return null; // 跳过生成
            }

            Vector2d normal = direction.GetNormal().RotateBy(Math.PI / 2) * (bufferWidth / 2);

            Point2d p1 = pt1 + normal;
            Point2d p2 = pt1 - normal;
            Point2d p3 = pt2 - normal;
            Point2d p4 = pt2 + normal;

            Polyline bufferPolyline = new Polyline(4);
            bufferPolyline.AddVertexAt(0, p1, 0, 0, 0);
            bufferPolyline.AddVertexAt(1, p2, 0, 0, 0);
            bufferPolyline.AddVertexAt(2, p3, 0, 0, 0);
            bufferPolyline.AddVertexAt(3, p4, 0, 0, 0);
            bufferPolyline.Closed = true;

            // 检查生成的 bufferPolyline 是否有效
            if (bufferPolyline.NumberOfVertices == 0)
            {
                throw new InvalidOperationException("缓冲区多段线未生成有效顶点。");
            }

            return bufferPolyline;
        }
        // 判断 Polyline 是否在矩形框内或与之相交
        private bool IsPolylineInsideOrIntersectingRectangle(Polyline polyline, Polyline rectangle)
        {
            // 检查 rectangle 是否为空或未初始化
            if (rectangle == null || rectangle.NumberOfVertices == 0)
            {
                throw new InvalidOperationException("Rectangle 未初始化或不包含顶点。");
            }

            // 检查 polyline 是否为空或未初始化
            if (polyline == null || polyline.NumberOfVertices == 0)
            {
                throw new InvalidOperationException("Polyline 未初始化或不包含顶点。");
            }

            // 检查 polyline 的每个顶点是否在 rectangle 内
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point3d polyPoint = polyline.GetPoint3dAt(i);
                if (IsPointInsideRectangle(polyPoint, rectangle))
                {
                    return true;
                }
            }

            // 检查 rectangle 的每个顶点是否在 polyline 内
            for (int i = 0; i < rectangle.NumberOfVertices; i++)
            {
                Point3d rectPoint = rectangle.GetPoint3dAt(i);
                if (IsPointInsidePolyline(rectPoint, polyline))
                {
                    return true;
                }
            }

            return false;
        }
        // 判断点是否在矩形内
        private bool IsPointInsideRectangle(Point3d pt, Polyline rectangle)
        {
            // 检查 rectangle 是否有效
            if (rectangle == null || rectangle.NumberOfVertices == 0)
            {
                return false; // 无效的 rectangle
            }

            // 尝试获取 GeometricExtents，并检查是否为 null
            Extents3d? rectExtents = rectangle.GeometricExtents;
            if (!rectExtents.HasValue)
            {
                throw new InvalidOperationException("无法获取 Polyline 的 GeometricExtents。");
            }

            return (pt.X >= rectExtents.Value.MinPoint.X && pt.X <= rectExtents.Value.MaxPoint.X &&
                    pt.Y >= rectExtents.Value.MinPoint.Y && pt.Y <= rectExtents.Value.MaxPoint.Y);
        }
        // 判断点是否在 Polyline 内部（用于多边形相交检测）
        private bool IsPointInsidePolyline(Point3d pt, Polyline polyline)
        {
            if (!polyline.Closed) return false;
            Point2d testPoint = new Point2d(pt.X, pt.Y);

            int numPoints = polyline.NumberOfVertices;
            Point2dCollection polygonPoints = new Point2dCollection();
            for (int i = 0; i < numPoints; i++)
            {
                polygonPoints.Add(polyline.GetPoint2dAt(i));
            }

            return IsPointInPolygon(testPoint, polygonPoints);
        }
        // 判断点是否在多边形内
        private bool IsPointInPolygon(Point2d pt, Point2dCollection poly)
        {
            int wn = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Point2d p1 = poly[i];
                Point2d p2 = poly[(i + 1) % poly.Count];
                if (p1.Y <= pt.Y)
                {
                    if (p2.Y > pt.Y && IsLeft(p1, p2, pt) > 0) wn++;
                }
                else if (p2.Y <= pt.Y && IsLeft(p1, p2, pt) < 0) wn--;
            }
            return wn != 0;
        }
        private double IsLeft(Point2d P0, Point2d P1, Point2d P2)
        {
            return ((P1.X - P0.X) * (P2.Y - P0.Y) - (P2.X - P0.X) * (P1.Y - P0.Y));
        }




        // 深度优先搜索查找所有相连的 Polyline
        private void DFS(ObjectId startId, HashSet<ObjectId> visited, Transaction tr)
        {
            Stack<ObjectId> stack = new Stack<ObjectId>();
            stack.Push(startId);

            while (stack.Count > 0)
            {
                ObjectId currentId = stack.Pop();
                if (visited.Contains(currentId)) continue;

                visited.Add(currentId);

                Polyline polyline = tr.GetObject(currentId, OpenMode.ForRead) as Polyline;
                if (polyline == null) continue;

                List<ObjectId> connectedIds = GetConnectedPolylines(polyline, tr);

                foreach (ObjectId connId in connectedIds)
                {
                    if (!visited.Contains(connId))
                    {
                        stack.Push(connId);
                    }
                }
            }
        }

        // 获取与指定 Polyline 相连的所有 Polyline 的 ObjectId
        private List<ObjectId> GetConnectedPolylines(Polyline polyline, Transaction tr)
        {
            List<ObjectId> result = new List<ObjectId>();

            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(HostApplicationServices.WorkingDatabase.CurrentSpaceId, OpenMode.ForRead);

            foreach (ObjectId objId in btr)
            {
                if (objId.IsErased || !objId.IsValid || objId == polyline.ObjectId)
                {
                    continue;
                }

                if (!objId.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(Polyline))))
                {
                    continue;
                }

                Polyline otherPolyline = tr.GetObject(objId, OpenMode.ForRead) as Polyline;
                if (otherPolyline != null && ArePolylinesConnected(polyline, otherPolyline))
                {
                    result.Add(otherPolyline.ObjectId);
                }
            }
            return result;
        }

        // 判断两条 Polyline 是否相连
        private bool ArePolylinesConnected(Polyline polyline1, Polyline polyline2)
        {
            return polyline1.StartPoint.IsEqualTo(polyline2.StartPoint) ||
                   polyline1.StartPoint.IsEqualTo(polyline2.EndPoint) ||
                   polyline1.EndPoint.IsEqualTo(polyline2.StartPoint) ||
                   polyline1.EndPoint.IsEqualTo(polyline2.EndPoint);
        }



        //恢复删除的主干线
        public void RestoreDeletedCurves(object sender, EventArgs e)
        {
            try
            {
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite);

                    foreach (Entity deletedEntity in deletedEntities)
                    {
                        // 确保已删除的实体被正确克隆，并恢复到模型空间
                        //Entity clonedEntity = (Entity)deletedEntity.Clone();

                        // 将克隆的实体重新添加到模型空间
                        btr.AppendEntity(deletedEntity);
                        tr.AddNewlyCreatedDBObject(deletedEntity, true);
                    }

                    tr.Commit();
                    MessageBox.Show("已恢复删除的曲线。");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"恢复删除的曲线时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        /* //像素法-DFS
         public void button1_Click(object sender, EventArgs e)
         {
             // 获取选中的 Polyline
             if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
             {
                 MessageBox.Show("请先选择一条 Polyline 作为中轴线。");
                 return;
             }

             // 从 Global 变量中获取指定区域的对角点坐标，用于生成像素块的范围
             double xMin = Math.Min(Global.x1, Global.x2);
             double yMin = Math.Min(Global.y1, Global.y2);
             double xMax = Math.Max(Global.x1, Global.x2);
             double yMax = Math.Max(Global.y1, Global.y2);

             int interval_pixel = 10; // 像素块大小
             double interval = 5; // 曲线采样点间隔

             // 初始化四叉树，用于存储像素块
             QuadTreeNode<Polyline> qtree = new QuadTreeNode<Polyline>(xMin, yMin, xMax - xMin, yMax - yMin, 5, 1);

             Database db = HostApplicationServices.WorkingDatabase;

             try
             {
                 using (Transaction tr = db.TransactionManager.StartTransaction())
                 {
                     BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                     // 生成区域内的像素块并插入四叉树
                     for (int i = 0; i <= (int)((xMax - xMin) / interval_pixel); i++)
                     {
                         for (int j = 0; j <= (int)((yMax - yMin) / interval_pixel); j++)
                         {
                             Point2d position = new Point2d(xMin + i * interval_pixel, yMin + j * interval_pixel);
                             Polyline pixelBlock = new Polyline();
                             pixelBlock.AddVertexAt(0, position, 0, 0, 0);
                             pixelBlock.AddVertexAt(1, new Point2d(position.X + interval_pixel, position.Y), 0, 0, 0);
                             pixelBlock.AddVertexAt(2, new Point2d(position.X + interval_pixel, position.Y + interval_pixel), 0, 0, 0);
                             pixelBlock.AddVertexAt(3, new Point2d(position.X, position.Y + interval_pixel), 0, 0, 0);
                             pixelBlock.Closed = true;
                             pixelBlock.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 7); // 白色

                             btr.AppendEntity(pixelBlock);
                             tr.AddNewlyCreatedDBObject(pixelBlock, true);

                             qtree.AddNode(position, pixelBlock); // 将像素块插入四叉树
                         }
                     }

                     // 查找选定的 Polyline 和所有相连的曲线
                     HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                     DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                     // 遍历每条相连的曲线，查找附近像素块并标记为绿色
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             double curveLength = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
                             int samplePointsCount = (int)(curveLength / interval);

                             for (int i = 0; i <= samplePointsCount; i++)
                             {
                                 // 获取采样点
                                 Point3d samplePoint = curve.GetPointAtDist(i * interval);
                                 var position = new Point3d(samplePoint.X - interval_pixel, samplePoint.Y - interval_pixel, 0);
                                 Rect searchRect = new Rect(position.X, position.Y, interval_pixel, interval_pixel);

                                 List<Polyline> nearbyPixels = new List<Polyline>();
                                 qtree.GetNode(searchRect, ref nearbyPixels);

                                 // 标记查找到的像素块为绿色
                                 foreach (var pixel in nearbyPixels)
                                 {
                                     pixel.UpgradeOpen();
                                     pixel.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色
                                 }
                             }
                         }
                     }

                     tr.Commit();
                     MessageBox.Show("查找并标记相连曲线附近的像素块完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                 }
             }
             catch (System.Exception ex)
             {
                 MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }

         public void button2_Click(object sender, EventArgs e)
         {
         // 获取选中的 Polyline
         if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
         {
             MessageBox.Show("请先选择一条 Polyline 作为中轴线。");
             return;
         }

         // 从 Global 变量中获取指定区域的对角点坐标，用于生成像素块的范围
         double xMin = Math.Min(Global.x1, Global.x2);
         double yMin = Math.Min(Global.y1, Global.y2);
         double xMax = Math.Max(Global.x1, Global.x2);
         double yMax = Math.Max(Global.y1, Global.y2);

         int interval_pixel = 10; // 像素块大小
         double interval = 5; // 曲线采样点间隔

         // 初始化四叉树，用于存储像素块
         QuadTreeNode<Polyline> qtree = new QuadTreeNode<Polyline>(xMin, yMin, xMax - xMin, yMax - yMin, 5, 1);
         Database db = HostApplicationServices.WorkingDatabase;

         try
         {
             using (Transaction tr = db.TransactionManager.StartTransaction())
             {
                 BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                 // 生成区域内的像素块并插入四叉树
                 for (int i = 0; i <= (int)((xMax - xMin) / interval_pixel); i++)
                 {
                     for (int j = 0; j <= (int)((yMax - yMin) / interval_pixel); j++)
                     {
                         Point2d position = new Point2d(xMin + i * interval_pixel, yMin + j * interval_pixel);
                         Polyline pixelBlock = CreatePixelBlock(position, interval_pixel);
                         btr.AppendEntity(pixelBlock);
                         tr.AddNewlyCreatedDBObject(pixelBlock, true);
                         qtree.AddNode(position, pixelBlock); // 将像素块插入四叉树
                     }
                 }

                 // 查找选定的 Polyline 和所有相连的曲线
                 HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                 DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                 // 存储所有相交的绿色像素块（线程安全集合）
                 ConcurrentBag<Polyline> greenPixels = new ConcurrentBag<Polyline>();

                 // 多线程并行查找附近像素块
                 Parallel.ForEach(connectedPolylines, polylineId =>
                 {
                     Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                     if (curve != null)
                     {
                         double curveLength = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
                         int samplePointsCount = (int)(curveLength / interval);

                         HashSet<Point3d> sampledPoints = new HashSet<Point3d>();

                         for (int i = 0; i <= samplePointsCount; i++)
                         {
                             Point3d samplePoint = curve.GetPointAtDist(i * interval);
                             if (sampledPoints.Contains(samplePoint)) continue;

                             sampledPoints.Add(samplePoint);
                             var position = new Point3d(samplePoint.X - interval_pixel, samplePoint.Y - interval_pixel, 0);
                             Rect searchRect = new Rect(position.X, position.Y, interval_pixel, interval_pixel);

                             List<Polyline> nearbyPixels = new List<Polyline>();
                             qtree.GetNode(searchRect, ref nearbyPixels);

                             foreach (var pixel in nearbyPixels)
                             {
                                 if (!greenPixels.Contains(pixel))
                                 {
                                     pixel.UpgradeOpen();
                                     pixel.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色
                                     greenPixels.Add(pixel);
                                 }
                             }
                         }
                     }
                 });

                 // 多线程并行遍历模型空间中的所有曲线，并隐藏与绿色像素块相交的曲线
                 Parallel.ForEach(btr.Cast<ObjectId>(), objId =>
                 {
                     Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                     if (entity is Curve modelCurve)
                     {
                         foreach (var greenPixel in greenPixels)
                         {
                             if (modelCurve.Bounds.HasValue && greenPixel.Bounds.HasValue &&
                                 ExtentsOverlap(modelCurve.Bounds.Value, greenPixel.Bounds.Value))
                             {
                                 entity.UpgradeOpen();
                                 entity.Visible = false; // 隐藏曲线
                                 break;
                             }
                         }
                     }
                 });

                 tr.Commit();
                 MessageBox.Show("查找并隐藏相交曲线操作完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
             }
         }
         catch (System.Exception ex)
         {
             MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
         }
     }//多线程报错

         public void button3_Click(object sender, EventArgs e)
         {
             // 获取选中的 Polyline
             if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
             {
                 MessageBox.Show("请先选择一条 Polyline 作为中轴线。");
                 return;
             }

             // 从 Global 变量中获取指定区域的对角点坐标，用于生成像素块的范围
             double xMin = Math.Min(Global.x1, Global.x2);
             double yMin = Math.Min(Global.y1, Global.y2);
             double xMax = Math.Max(Global.x1, Global.x2);
             double yMax = Math.Max(Global.y1, Global.y2);

             int interval_pixel = 10; // 像素块大小
             double interval = 5; // 曲线采样点间隔

             // 初始化四叉树，用于存储像素块
             QuadTreeNode<Polyline> qtree = new QuadTreeNode<Polyline>(xMin, yMin, xMax - xMin, yMax - yMin, 5, 1);
             Database db = HostApplicationServices.WorkingDatabase;

             try
             {
                 using (Transaction tr = db.TransactionManager.StartTransaction())
                 {
                     BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                     // 生成区域内的像素块并插入四叉树
                     for (int i = 0; i <= (int)((xMax - xMin) / interval_pixel); i++)
                     {
                         for (int j = 0; j <= (int)((yMax - yMin) / interval_pixel); j++)
                         {
                             Point2d position = new Point2d(xMin + i * interval_pixel, yMin + j * interval_pixel);
                             Polyline pixelBlock = CreatePixelBlock(position, interval_pixel);
                             btr.AppendEntity(pixelBlock);
                             tr.AddNewlyCreatedDBObject(pixelBlock, true);
                             qtree.AddNode(position, pixelBlock); // 将像素块插入四叉树
                         }
                     }

                     // 查找选定的 Polyline 和所有相连的曲线
                     HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                     DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                     // 用于存储所有相交的绿色像素块
                     HashSet<Polyline> greenPixels = new HashSet<Polyline>();

                     // 遍历每条相连的曲线，查找附近像素块并标记为绿色
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             double curveLength = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
                             int samplePointsCount = (int)(curveLength / interval);

                             // 优化：减少每个曲线的采样次数
                             HashSet<Point3d> sampledPoints = new HashSet<Point3d>();

                             for (int i = 0; i <= samplePointsCount; i++)
                             {
                                 // 获取采样点
                                 Point3d samplePoint = curve.GetPointAtDist(i * interval);
                                 if (sampledPoints.Contains(samplePoint)) continue;

                                 sampledPoints.Add(samplePoint);
                                 var position = new Point3d(samplePoint.X - interval_pixel, samplePoint.Y - interval_pixel, 0);
                                 Rect searchRect = new Rect(position.X, position.Y, interval_pixel, interval_pixel);

                                 // 查询与该采样点相交的像素块
                                 List<Polyline> nearbyPixels = new List<Polyline>();
                                 qtree.GetNode(searchRect, ref nearbyPixels);

                                 // 标记查找到的像素块为绿色
                                 foreach (var pixel in nearbyPixels)
                                 {
                                     if (!greenPixels.Contains(pixel))
                                     {
                                         pixel.UpgradeOpen();
                                         pixel.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色
                                         greenPixels.Add(pixel); // 将绿色像素块加入集合
                                     }
                                 }
                             }
                         }
                     }

                     // 遍历模型空间中的所有曲线，并隐藏与绿色像素块相交的曲线
                     foreach (ObjectId objId in btr)
                     {
                         Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                         if (entity is Curve modelCurve)
                         {
                             // 通过 Bounds 预先检查是否可能相交
                             foreach (var greenPixel in greenPixels)
                             {
                                 if (modelCurve.Bounds.HasValue && greenPixel.Bounds.HasValue &&
                                     ExtentsOverlap(modelCurve.Bounds.Value, greenPixel.Bounds.Value))
                                 {
                                     entity.UpgradeOpen();
                                     entity.Visible = false; // 隐藏曲线
                                     break; // 一旦找到相交的绿色像素块，立即跳出循环
                                 }
                             }
                         }
                     }

                     tr.Commit();
                     MessageBox.Show("查找并隐藏相交曲线操作完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                 }
             }
             catch (System.Exception ex)
             {
                 MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }//优化无果

         // 创建像素块
         private Polyline CreatePixelBlock(Point2d position, int intervalPixel)
         {
             Polyline pixelBlock = new Polyline();
             pixelBlock.AddVertexAt(0, position, 0, 0, 0);
             pixelBlock.AddVertexAt(1, new Point2d(position.X + intervalPixel, position.Y), 0, 0, 0);
             pixelBlock.AddVertexAt(2, new Point2d(position.X + intervalPixel, position.Y + intervalPixel), 0, 0, 0);
             pixelBlock.AddVertexAt(3, new Point2d(position.X, position.Y + intervalPixel), 0, 0, 0);
             pixelBlock.Closed = true;
             pixelBlock.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 7); // 白色
             return pixelBlock;
         }

         public void button4_Click(object sender, EventArgs e)
         {
             // 获取选中的 Polyline
             if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
             {
                 MessageBox.Show("请先选择一条 Polyline 作为中轴线。");
                 return;
             }

             // 从 Global 变量中获取指定区域的对角点坐标，用于生成像素块的范围
             double xMin = Math.Min(Global.x1, Global.x2);
             double yMin = Math.Min(Global.y1, Global.y2);
             double xMax = Math.Max(Global.x1, Global.x2);
             double yMax = Math.Max(Global.y1, Global.y2);

             int interval_pixel = 10; // 像素块大小
             double interval = 5; // 曲线采样点间隔

             // 初始化四叉树，用于存储像素块
             QuadTreeNode<Polyline> qtree = new QuadTreeNode<Polyline>(xMin, yMin, xMax - xMin, yMax - yMin, 5, 1);
             Database db = HostApplicationServices.WorkingDatabase;

             try
             {
                 using (Transaction tr = db.TransactionManager.StartTransaction())
                 {
                     BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                     // 生成区域内的像素块并插入四叉树
                     for (int i = 0; i <= (int)((xMax - xMin) / interval_pixel); i++)
                     {
                         for (int j = 0; j <= (int)((yMax - yMin) / interval_pixel); j++)
                         {
                             Point2d position = new Point2d(xMin + i * interval_pixel, yMin + j * interval_pixel);
                             Polyline pixelBlock = new Polyline();
                             pixelBlock.AddVertexAt(0, position, 0, 0, 0);
                             pixelBlock.AddVertexAt(1, new Point2d(position.X + interval_pixel, position.Y), 0, 0, 0);
                             pixelBlock.AddVertexAt(2, new Point2d(position.X + interval_pixel, position.Y + interval_pixel), 0, 0, 0);
                             pixelBlock.AddVertexAt(3, new Point2d(position.X, position.Y + interval_pixel), 0, 0, 0);
                             //pixelBlock.Closed = true;
                            // pixelBlock.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 7); // 白色

                             //btr.AppendEntity(pixelBlock);
                             //tr.AddNewlyCreatedDBObject(pixelBlock, true);

                             qtree.AddNode(position, pixelBlock); // 将像素块插入四叉树
                         }
                     }

                     // 查找选定的 Polyline 和所有相连的曲线
                     HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                     DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                     // 遍历每条相连的曲线，查找附近像素块并标记为绿色
                     HashSet<Polyline> greenPixels = new HashSet<Polyline>();
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             double curveLength = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
                             int samplePointsCount = (int)(curveLength / interval);

                             for (int i = 0; i <= samplePointsCount; i++)
                             {
                                 // 获取采样点
                                 Point3d samplePoint = curve.GetPointAtDist(i * interval);
                                 var position = new Point3d(samplePoint.X - interval_pixel, samplePoint.Y - interval_pixel, 0);
                                 Rect searchRect = new Rect(position.X, position.Y , interval_pixel, interval_pixel);

                                 List<Polyline> nearbyPixels = new List<Polyline>();
                                 qtree.GetNode(searchRect, ref nearbyPixels);

                                 // 标记查找到的像素块为绿色
                                 foreach (var pixel in nearbyPixels)
                                 {
                                     pixel.UpgradeOpen();
                                     pixel.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色
                                     greenPixels.Add(pixel); // 将绿色像素块加入集合
                                 }
                             }
                         }
                     }

                     // 遍历模型空间中的所有曲线，并隐藏与绿色像素块相交的曲线
                     foreach (ObjectId objId in btr)
                     {
                         Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                         if (entity is Curve modelCurve)
                         {
                             foreach (var greenPixel in greenPixels)
                             {
                                 if (modelCurve.Bounds.HasValue && greenPixel.Bounds.HasValue &&
                                     ExtentsOverlap(modelCurve.Bounds.Value, greenPixel.Bounds.Value))
                                 {
                                     entity.UpgradeOpen();
                                     entity.Erase(); // 删除曲线
                                     break; // 一旦找到相交的绿色像素块，立即跳出循环 
                                 }

                             }
                         }
                     }

                     tr.Commit();
                     MessageBox.Show("查找并隐藏相交曲线操作完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                 }
             }
             catch (System.Exception ex)
             {
                 MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }//像素法 第一次时间7分钟 第二次4.52分钟

         // 辅助方法，用于检查两个 Extents3d 对象是否重叠
         private bool ExtentsOverlap(Extents3d ext1, Extents3d ext2)
         {
             // 检查是否在 X 方向上有重叠
             bool xOverlap = ext1.MaxPoint.X >= ext2.MinPoint.X && ext1.MinPoint.X <= ext2.MaxPoint.X;

             // 检查是否在 Y 方向上有重叠
             bool yOverlap = ext1.MaxPoint.Y >= ext2.MinPoint.Y && ext1.MinPoint.Y <= ext2.MaxPoint.Y;

             // 检查是否在 Z 方向上有重叠（如果不需要 Z 方向，可以省略）
             bool zOverlap = ext1.MaxPoint.Z >= ext2.MinPoint.Z && ext1.MinPoint.Z <= ext2.MaxPoint.Z;

             // 只有在三个方向上都有重叠，才认为两个 Extents3d 对象重叠
             return xOverlap && yOverlap && zOverlap;
         }*/

        /*//DFS退而求其次 只删除DFS曲线 待测试效果
         public void button_HideCurves_Click(object sender, EventArgs e)
         {
             // 检查是否选择了有效的 Polyline
             if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
             {
                 MessageBox.Show("请先选择一条 Polyline 作为中轴线。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }

             try
             {
                 using (Transaction tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
                 {
                     // 获取模型空间
                     BlockTableRecord btr = (BlockTableRecord)tr.GetObject(HostApplicationServices.WorkingDatabase.CurrentSpaceId, OpenMode.ForWrite);

                     // 使用 DFS 查找所有直接或间接相连的曲线
                     HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                     DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                     // 定义四叉树边界
                     double xMin = double.MaxValue, yMin = double.MaxValue;
                     double xMax = double.MinValue, yMax = double.MinValue;

                     // 初始化四叉树，并确定插入四叉树的范围
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             Extents3d extents = curve.GeometricExtents;
                             xMin = Math.Min(xMin, extents.MinPoint.X);
                             yMin = Math.Min(yMin, extents.MinPoint.Y);
                             xMax = Math.Max(xMax, extents.MaxPoint.X);
                             yMax = Math.Max(yMax, extents.MaxPoint.Y);
                         }
                     }

                     QuadTreeNode<ObjectId> quadTree = new QuadTreeNode<ObjectId>(xMin, yMin, xMax - xMin, yMax - yMin, 5, 1);

                     // 将所有相连曲线的包围盒插入四叉树中
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             Extents3d extents = curve.GeometricExtents;
                             Point2d center = new Point2d((extents.MinPoint.X + extents.MaxPoint.X) / 2,
                                                           (extents.MinPoint.Y + extents.MaxPoint.Y) / 2);
                             quadTree.AddNode(center, polylineId);
                         }
                     }

                     HashSet<ObjectId> curvesToHide = new HashSet<ObjectId>(connectedPolylines);

                     // 遍历四叉树中的每条曲线，检查是否与模型空间其他曲线相交
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Curve curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                         if (curve != null)
                         {
                             Extents3d curveExtents = curve.GeometricExtents;
                             Rect searchArea = new Rect(curveExtents.MinPoint.X, curveExtents.MinPoint.Y,
                                                        curveExtents.MaxPoint.X - curveExtents.MinPoint.X,
                                                        curveExtents.MaxPoint.Y - curveExtents.MinPoint.Y);

                             List<ObjectId> possibleIntersections = new List<ObjectId>();
                             quadTree.GetNode(searchArea, ref possibleIntersections);

                             foreach (ObjectId possibleId in possibleIntersections)
                             {
                                 if (curvesToHide.Contains(possibleId)) continue;

                                 Curve modelCurve = tr.GetObject(possibleId, OpenMode.ForRead) as Curve;
                                 if (modelCurve != null && ExtentsOverlap(curve.GeometricExtents, modelCurve.GeometricExtents))
                                 {
                                     curvesToHide.Add(possibleId);
                                 }
                             }
                         }
                     }

                     // 隐藏所有找到的相连及相交曲线
                     foreach (ObjectId curveId in curvesToHide)
                     {
                         Entity ent = tr.GetObject(curveId, OpenMode.ForWrite) as Entity;
                         if (ent != null)
                         {
                             ent.Visible = false; // 设置为不可见
                         }
                     }

                     tr.Commit();
                     MessageBox.Show("相连的曲线及其相交曲线已隐藏完成！");
                 }
             }
             catch (System.Exception ex)
             {
                 MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }//使用四叉树，时间2分10几秒，同时删除了与DFS相交的曲线

         //更次
         public void button_DeleteConnectedCurves_Click(object sender, EventArgs e)
         {
             // 检查是否选择了有效的 Polyline
             if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
             {
                 MessageBox.Show("请先选择一条 Polyline 作为中轴线。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }

             try
             {
                 using (Transaction tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
                 {
                     // 获取模型空间
                     BlockTableRecord btr = (BlockTableRecord)tr.GetObject(HostApplicationServices.WorkingDatabase.CurrentSpaceId, OpenMode.ForWrite);

                     // 使用 DFS 查找所有直接或间接相连的曲线
                     HashSet<ObjectId> connectedPolylines = new HashSet<ObjectId>();
                     DFS(GlobalEntityData.SelectedEntityId, connectedPolylines, tr);

                     // 遍历找到的曲线并将其隐藏
                     foreach (ObjectId polylineId in connectedPolylines)
                     {
                         Entity entity = tr.GetObject(polylineId, OpenMode.ForWrite) as Entity;
                         if (entity != null)
                         {
                             entity.Visible = false; // 设置为不可见
                         }
                     }

                     tr.Commit();
                     MessageBox.Show("相连的曲线已隐藏完成！");
                 }
             }
             catch (System.Exception ex)
             {
                 MessageBox.Show($"发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }//只删除DFS找到的曲线*/






        //包围盒

        /*        public void InsertCurvesInQuadTree(object sender, EventArgs e)
        {
            // 使用Global变量中的对角点定义筛选区域
            double xMin = Math.Min(Global.x1, Global.x2);
            double yMin = Math.Min(Global.y1, Global.y2);
            double xMax = Math.Max(Global.x1, Global.x2);
            double yMax = Math.Max(Global.y1, Global.y2);

            // 定义选择区域
            Extents3d selectionRect = new Extents3d(new Point3d(xMin, yMin, 0), new Point3d(xMax, yMax, 0));

            // 初始化曲线列表和四叉树
            List<Curve> curves = new List<Curve>();
            //QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 0.5);

            // 访问当前数据库
            Database db = HostApplicationServices.WorkingDatabase;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    // 遍历模型空间中的曲线，并筛选出在矩形范围内的曲线
                    foreach (ObjectId objId in btr)
                    {
                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity is Curve curve)
                        {
                            // 获取曲线的包围盒
                            Extents3d? curveBounds = curve.Bounds;

                            // 检查曲线包围盒是否在选择区域内
                            if (curveBounds.HasValue &&
                                selectionRect.MinPoint.X <= curveBounds.Value.MaxPoint.X &&
                                selectionRect.MaxPoint.X >= curveBounds.Value.MinPoint.X &&
                                selectionRect.MinPoint.Y <= curveBounds.Value.MaxPoint.Y &&
                                selectionRect.MaxPoint.Y >= curveBounds.Value.MinPoint.Y)
                            {
                                curves.Add(curve);

                            }
                        }
                    }
                    tr.Commit();
                }

                // 分组存储
                List<List<Curve>> curveGroups = GroupCurvesByIntersection(curves);

                // 绘制每组曲线的整体包围盒
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var group in curveGroups)
                    {
                        List<Point3d> groupPoints = new List<Point3d>();

                        foreach (var curve in group)
                        {
                            Extents3d extents = curve.GeometricExtents;
                            groupPoints.Add(extents.MinPoint);
                            groupPoints.Add(extents.MaxPoint);
                        }

                        // 计算组的整体包围盒

                        var xMinGroup = groupPoints.Min(pt => pt.X);
                        var xMaxGroup = groupPoints.Max(pt => pt.X);
                        var yMinGroup = groupPoints.Min(pt => pt.Y);
                        var yMaxGroup = groupPoints.Max(pt => pt.Y);

                        // 绘制整体包围盒的外轮廓
                        Polyline groupOutline = new Polyline();
                        groupOutline.AddVertexAt(0, new Point2d(xMinGroup, yMinGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(1, new Point2d(xMaxGroup, yMinGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(2, new Point2d(xMaxGroup, yMaxGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(3, new Point2d(xMinGroup, yMaxGroup), 0, 0, 0);
                        groupOutline.Closed = true;
                        groupOutline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 绿色

                        // 将外轮廓插入到模型空间中
                        btr.AppendEntity(groupOutline);
                        tr.AddNewlyCreatedDBObject(groupOutline, true);
                    }

                    tr.Commit();
                }

            }
            catch (System.Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }//解决了最外面的包围盒 时间长

        // 分组函数：将重叠包围盒的曲线分为同一组
        private List<List<Curve>> GroupCurvesByIntersection(List<Curve> curves)
        {
            List<List<Curve>> curveGroups = new List<List<Curve>>();

            foreach (var curve in curves)
            {
                Extents3d currentBox = curve.GeometricExtents;
                List<List<Curve>> intersectingGroups = new List<List<Curve>>();

                // 检查与现有组是否相交
                foreach (var group in curveGroups)
                {
                    foreach (var groupCurve in group)
                    {
                        if (OverlapsOrContains(currentBox, groupCurve.GeometricExtents))
                        {
                            intersectingGroups.Add(group); // 找到相交的组
                            break;
                        }
                    }
                }

                // 合并所有相交的组并加入当前曲线
                if (intersectingGroups.Count > 0)
                {
                    List<Curve> mergedGroup = new List<Curve> { curve };
                    foreach (var group in intersectingGroups)
                    {
                        mergedGroup.AddRange(group);
                        curveGroups.Remove(group);
                    }
                    curveGroups.Add(mergedGroup);
                }
                else
                {
                    curveGroups.Add(new List<Curve> { curve });
                }
            }

            return curveGroups;
        }

        // 判断两个 Extents3d 是否重叠
        private bool OverlapsOrContains(Extents3d box1, Extents3d box2)
        {
            // 检查是否有重叠区域
            bool xOverlap = box1.MaxPoint.X >= box2.MinPoint.X && box1.MinPoint.X <= box2.MaxPoint.X;
            bool yOverlap = box1.MaxPoint.Y >= box2.MinPoint.Y && box1.MinPoint.Y <= box2.MaxPoint.Y;

            // 如果有重叠，返回 true
            if (xOverlap && yOverlap) return true;

            // 检查 box1 是否完全包含 box2
            bool box1ContainsBox2 =
                box1.MinPoint.X <= box2.MinPoint.X && box1.MaxPoint.X >= box2.MaxPoint.X &&
                box1.MinPoint.Y <= box2.MinPoint.Y && box1.MaxPoint.Y >= box2.MaxPoint.Y;

            // 检查 box2 是否完全包含 box1
            bool box2ContainsBox1 =
                box2.MinPoint.X <= box1.MinPoint.X && box2.MaxPoint.X >= box1.MaxPoint.X &&
                box2.MinPoint.Y <= box1.MinPoint.Y && box2.MaxPoint.Y >= box1.MaxPoint.Y;

            return box1ContainsBox2 || box2ContainsBox1;
        }*/

        /*        public void InsertCurvesInQuadTree1(object sender, EventArgs e)
        {
            double xMin = Math.Min(Global.x1, Global.x2);
            double yMin = Math.Min(Global.y1, Global.y2);
            double xMax = Math.Max(Global.x1, Global.x2);
            double yMax = Math.Max(Global.y1, Global.y2);

            Extents3d selectionRect = new Extents3d(new Point3d(xMin, yMin, 0), new Point3d(xMax, yMax, 0));
            List<Curve> curves = new List<Curve>();

            // 优化四叉树初始化，使其覆盖全局范围
            QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10,1);

            Database db = HostApplicationServices.WorkingDatabase;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    foreach (ObjectId objId in btr)
                    {
                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity is Curve curve)
                        {
                            Extents3d? curveBounds = curve.Bounds;

                            if (curveBounds.HasValue && selectionRect.MinPoint.X <= curveBounds.Value.MaxPoint.X &&
                                selectionRect.MaxPoint.X >= curveBounds.Value.MinPoint.X &&
                                selectionRect.MinPoint.Y <= curveBounds.Value.MaxPoint.Y &&
                                selectionRect.MaxPoint.Y >= curveBounds.Value.MinPoint.Y)
                            {
                                curves.Add(curve);
                                quadTree.AddNode(curve.GeometricExtents.MinPoint, curve); // 插入四叉树
                            }
                        }
                    }
                    tr.Commit();
                }

                double bufferDistance = 55.0; // 您可以根据需要设置缓冲区的大小
                List<List<Curve>> initialGroups = GroupCurvesByIntersection(curves, quadTree, bufferDistance);


                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var group in initialGroups)
                    {
                        // 获取当前组的所有点来生成整体包围盒
                        List<Point3d> groupPoints = new List<Point3d>();

                        foreach (var curve in group)
                        {
                            Extents3d extents = curve.GeometricExtents;
                            groupPoints.Add(extents.MinPoint);
                            groupPoints.Add(extents.MaxPoint);
                        }

                        // 计算当前组的整体包围盒
                        double xMinGroup = groupPoints.Min(pt => pt.X);
                        double xMaxGroup = groupPoints.Max(pt => pt.X);
                        double yMinGroup = groupPoints.Min(pt => pt.Y);
                        double yMaxGroup = groupPoints.Max(pt => pt.Y);

                        // 绘制整体包围盒的外轮廓
                        Polyline groupOutline = new Polyline();
                        groupOutline.AddVertexAt(0, new Point2d(xMinGroup, yMinGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(1, new Point2d(xMaxGroup, yMinGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(2, new Point2d(xMaxGroup, yMaxGroup), 0, 0, 0);
                        groupOutline.AddVertexAt(3, new Point2d(xMinGroup, yMaxGroup), 0, 0, 0);
                        groupOutline.Closed = true;
                        groupOutline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 绿色

                        // 将外轮廓插入到模型空间中
                        btr.AppendEntity(groupOutline);
                        tr.AddNewlyCreatedDBObject(groupOutline, true);
                    }

                    tr.Commit();
                }


            }
            catch (System.Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }//四叉树法，并且给包围盒加缓冲区（宽度可自定义 55最好），效果不错了

        // 定义曲线组
        public class CurveGroup
        {
            public List<Curve> Curves { get; set; }
            public Extents3d BoundingBox { get; set; }

            public CurveGroup(List<Curve> curves, Extents3d boundingBox)
            {
                Curves = curves;
                BoundingBox = boundingBox;
            }
        }

        // 使用四叉树和分组函数，确保包围盒不嵌套
        private List<List<Curve>> GroupCurvesByIntersection(List<Curve> curves, QuadTreeNode<Curve> quadTree, double bufferDistance)
        {
            List<List<Curve>> curveGroups = new List<List<Curve>>();
            HashSet<Curve> processedCurves = new HashSet<Curve>();

            foreach (var curve in curves)
            {
                // 如果曲线已处理，跳过
                if (processedCurves.Contains(curve)) continue;

                // 当前曲线的包围盒
                Extents3d currentBox = curve.GeometricExtents;

                // 创建当前组
                List<Curve> currentGroup = new List<Curve> { curve };
                processedCurves.Add(curve);

                // 创建搜索区域（缓冲区扩大）
                Extents3d searchBox = new Extents3d(
                    new Point3d(currentBox.MinPoint.X - bufferDistance, currentBox.MinPoint.Y - bufferDistance, 0),
                    new Point3d(currentBox.MaxPoint.X + bufferDistance, currentBox.MaxPoint.Y + bufferDistance, 0)
                );

                // 四叉树查询相交候选集
                List<Curve> candidates = new List<Curve>();
                quadTree.GetNode(new Rect(searchBox.MinPoint.X, searchBox.MinPoint.Y, searchBox.MaxPoint.X - searchBox.MinPoint.X, searchBox.MaxPoint.Y - searchBox.MinPoint.Y), ref candidates);

                // 使用队列来处理相交曲线的查找
                Queue<Curve> queue = new Queue<Curve>(currentGroup);

                while (queue.Count > 0)
                {
                    Curve currentCurve = queue.Dequeue();
                    Extents3d box = currentCurve.GeometricExtents;

                    foreach (var candidate in candidates)
                    {
                        // 跳过已处理的候选曲线
                        if (processedCurves.Contains(candidate)) continue;

                        // 判断是否相交或包含
                        if (OverlapsOrContains1(box, candidate.GeometricExtents))
                        {
                            currentGroup.Add(candidate);
                            queue.Enqueue(candidate);
                            processedCurves.Add(candidate);
                        }
                    }
                }

                curveGroups.Add(currentGroup);
            }

            return curveGroups;
        }

        // 判断是否相交或包含关系
        private bool OverlapsOrContains1(Extents3d box1, Extents3d box2)
        {
            bool xOverlap = box1.MaxPoint.X >= box2.MinPoint.X && box1.MinPoint.X <= box2.MaxPoint.X;
            bool yOverlap = box1.MaxPoint.Y >= box2.MinPoint.Y && box1.MinPoint.Y <= box2.MaxPoint.Y;
            bool contains = (box1.MinPoint.X <= box2.MinPoint.X && box1.MaxPoint.X >= box2.MaxPoint.X &&
                             box1.MinPoint.Y <= box2.MinPoint.Y && box1.MaxPoint.Y >= box2.MaxPoint.Y);
            return (xOverlap && yOverlap) || contains;
        }*/

        /*        public void InsertCurvesAndDrawFinalBoundingBoxes(object sender, EventArgs e)
                {
                    // 定义选择区域
                    double xMin = Math.Min(Global.x1, Global.x2);
                    double yMin = Math.Min(Global.y1, Global.y2);
                    double xMax = Math.Max(Global.x1, Global.x2);
                    double yMax = Math.Max(Global.y1, Global.y2);

                    Extents3d selectionRect = new Extents3d(new Point3d(xMin, yMin, 0), new Point3d(xMax, yMax, 0));

                    // 初始化曲线列表和四叉树
                    List<Curve> curves = new List<Curve>();
                    QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 0.5);

                    Database db = HostApplicationServices.WorkingDatabase;

                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                            foreach (ObjectId objId in btr)
                            {
                                Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                                if (entity is Curve curve)
                                {
                                    Extents3d? curveBounds = curve.Bounds;

                                    if (curveBounds.HasValue && IsWithinSelection(curveBounds.Value, selectionRect))
                                    {
                                        curves.Add(curve);
                                        //Point3d center = CalculateCenter(curveBounds.Value);
                                        //quadTree.AddNode(center, curve);
                                        quadTree.AddNode(curve.GeometricExtents.MinPoint, curve); // 插入四叉树
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        // 初步分组
                        List<List<Curve>> initialGroups = GroupCurvesByIntersection(curves, quadTree, 10);

                        // 二次合并重叠的分组
                        List<List<Curve>> finalGroups = MergeOverlappingGroups(initialGroups);

                        // 绘制每组的最终包围盒
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                            foreach (var group in finalGroups)
                            {
                                Extents3d boundingBox = CalculateBoundingBox(group);
                                DrawBoundingBox(btr, tr, boundingBox);
                            }

                            tr.Commit();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show("错误：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }//二次合并分组

                // 判断曲线包围盒是否在选择区域内
                private bool IsWithinSelection(Extents3d curveBounds, Extents3d selectionRect)
                {
                    return curveBounds.MinPoint.X >= selectionRect.MinPoint.X &&
                           curveBounds.MaxPoint.X <= selectionRect.MaxPoint.X &&
                           curveBounds.MinPoint.Y >= selectionRect.MinPoint.Y &&
                           curveBounds.MaxPoint.Y <= selectionRect.MaxPoint.Y;
                }

                // 计算包围盒的中心点
                private Point3d CalculateCenter(Extents3d box)
                {
                    return new Point3d((box.MinPoint.X + box.MaxPoint.X) / 2, (box.MinPoint.Y + box.MaxPoint.Y) / 2, 0);
                }

                // 绘制单个包围盒
                private void DrawBoundingBox(BlockTableRecord btr, Transaction tr, Extents3d boundingBox)
                {
                    Polyline boundingPolyline = new Polyline();
                    boundingPolyline.AddVertexAt(0, new Point2d(boundingBox.MinPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(1, new Point2d(boundingBox.MaxPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(2, new Point2d(boundingBox.MaxPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(3, new Point2d(boundingBox.MinPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                    boundingPolyline.Closed = true;
                    boundingPolyline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 绿色

                    btr.AppendEntity(boundingPolyline);
                    tr.AddNewlyCreatedDBObject(boundingPolyline, true);
                }

                // 合并重叠的分组包围盒
                private List<List<Curve>> MergeOverlappingGroups(List<List<Curve>> initialGroups)
                {
                    List<List<Curve>> mergedGroups = new List<List<Curve>>();
                    HashSet<List<Curve>> processedGroups = new HashSet<List<Curve>>();

                    foreach (var group in initialGroups)
                    {
                        if (processedGroups.Contains(group)) continue;

                        List<Curve> mergedGroup = new List<Curve>(group);
                        processedGroups.Add(group);

                        Extents3d groupBoundingBox = CalculateBoundingBox(group);

                        foreach (var otherGroup in initialGroups)
                        {
                            if (group == otherGroup || processedGroups.Contains(otherGroup)) continue;

                            Extents3d otherBoundingBox = CalculateBoundingBox(otherGroup);

                            if (OverlapsOrContains1(groupBoundingBox, otherBoundingBox))
                            {
                                mergedGroup.AddRange(otherGroup);
                                groupBoundingBox = CalculateBoundingBox(mergedGroup); // 更新合并后的包围盒
                                processedGroups.Add(otherGroup);
                            }
                        }

                        mergedGroups.Add(mergedGroup);
                    }

                    return mergedGroups;
                }

                // 判断两个 Extents3d 是否重叠

                private bool Overlaps(Extents3d box1, Extents3d box2)
                {
                    bool xOverlap = box1.MaxPoint.X >= box2.MinPoint.X && box1.MinPoint.X <= box2.MaxPoint.X;
                    bool yOverlap = box1.MaxPoint.Y >= box2.MinPoint.Y && box1.MinPoint.Y <= box2.MaxPoint.Y;
                    return xOverlap && yOverlap;
                }

                // 计算组的整体包围盒
                private Extents3d CalculateBoundingBox(List<Curve> curves)
                {
                    Point3d minPoint = new Point3d(double.MaxValue, double.MaxValue, 0);
                    Point3d maxPoint = new Point3d(double.MinValue, double.MinValue, 0);

                    foreach (var curve in curves)
                    {
                        var extents = curve.GeometricExtents;
                        minPoint = new Point3d(
                            Math.Min(minPoint.X, extents.MinPoint.X),
                            Math.Min(minPoint.Y, extents.MinPoint.Y), 0);
                        maxPoint = new Point3d(
                            Math.Max(maxPoint.X, extents.MaxPoint.X),
                            Math.Max(maxPoint.Y, extents.MaxPoint.Y), 0);
                    }

                    return new Extents3d(minPoint, maxPoint);
                }*/

        /*        public void InsertCurvesAndDrawFullyMergedBoundingBoxes(object sender, EventArgs e)//二次分组再尝试，时间1分以内
                {
                    // 定义选择区域
                    double xMin = Math.Min(Global.x1, Global.x2);
                    double yMin = Math.Min(Global.y1, Global.y2);
                    double xMax = Math.Max(Global.x1, Global.x2);
                    double yMax = Math.Max(Global.y1, Global.y2);

                    Extents3d selectionRect = new Extents3d(new Point3d(xMin, yMin, 0), new Point3d(xMax, yMax, 0));

                    // 初始化曲线列表和四叉树
                    List<Curve> curves = new List<Curve>();
                    QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 0.5);

                    Database db = HostApplicationServices.WorkingDatabase;

                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                            // 遍历模型空间中的曲线，并将曲线插入四叉树
                            foreach (ObjectId objId in btr)
                            {
                                Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                                if (entity is Curve curve)
                                {
                                    Extents3d? curveBounds = curve.Bounds;

                                    if (curveBounds.HasValue && IsWithinSelection1(curveBounds.Value, selectionRect))
                                    {
                                        curves.Add(curve);
                                        quadTree.AddNode(curve.GeometricExtents.MinPoint, curve); // 插入四叉树
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        // 初步分组
                        List<List<Curve>> initialGroups = GroupCurvesByIntersection1(curves, quadTree, 0.05);

                        // 完全合并所有重叠或包含的分组
                        List<Extents3d> finalMergedBoundingBoxes = FullyMergeGroups(initialGroups);

                        // 绘制每组的最终包围盒
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                            foreach (var boundingBox in finalMergedBoundingBoxes)
                            {
                                DrawBoundingBox1(btr, tr, boundingBox);
                            }

                            tr.Commit();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show("错误：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // 判断曲线包围盒是否在选择区域内
                private bool IsWithinSelection1(Extents3d curveBounds, Extents3d selectionRect)
                {
                    return curveBounds.MinPoint.X >= selectionRect.MinPoint.X &&
                           curveBounds.MaxPoint.X <= selectionRect.MaxPoint.X &&
                           curveBounds.MinPoint.Y >= selectionRect.MinPoint.Y &&
                           curveBounds.MaxPoint.Y <= selectionRect.MaxPoint.Y;
                }

                // 绘制单个包围盒
                private void DrawBoundingBox1(BlockTableRecord btr, Transaction tr, Extents3d boundingBox)
                {
                    Polyline boundingPolyline = new Polyline();
                    boundingPolyline.AddVertexAt(0, new Point2d(boundingBox.MinPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(1, new Point2d(boundingBox.MaxPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(2, new Point2d(boundingBox.MaxPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                    boundingPolyline.AddVertexAt(3, new Point2d(boundingBox.MinPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                    boundingPolyline.Closed = true;
                    boundingPolyline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 绿色

                    btr.AppendEntity(boundingPolyline);
                    tr.AddNewlyCreatedDBObject(boundingPolyline, true);
                }

                // 通过四叉树和缓冲区进行初步分组
                private List<List<Curve>> GroupCurvesByIntersection1(List<Curve> curves, QuadTreeNode<Curve> quadTree, double bufferDistance)
                {
                    List<List<Curve>> curveGroups = new List<List<Curve>>();
                    HashSet<Curve> processedCurves = new HashSet<Curve>();

                    foreach (var curve in curves)
                    {
                        if (processedCurves.Contains(curve)) continue;

                        List<Curve> intersectingCurves = new List<Curve> { curve };
                        Queue<Curve> curveQueue = new Queue<Curve>();
                        curveQueue.Enqueue(curve);
                        processedCurves.Add(curve);

                        while (curveQueue.Count > 0)
                        {
                            Curve currentCurve = curveQueue.Dequeue();
                            Extents3d box = ExpandBounds1(currentCurve.GeometricExtents, bufferDistance);

                            List<Curve> candidates = new List<Curve>();
                            quadTree.GetNode(new Rect(box.MinPoint.X, box.MinPoint.Y, box.MaxPoint.X - box.MinPoint.X, box.MaxPoint.Y - box.MinPoint.Y), ref candidates);

                            foreach (var candidate in candidates)
                            {
                                if (!processedCurves.Contains(candidate) && Overlaps(box, candidate.GeometricExtents))
                                {
                                    intersectingCurves.Add(candidate);
                                    curveQueue.Enqueue(candidate);
                                    processedCurves.Add(candidate);
                                }
                            }
                        }

                        curveGroups.Add(intersectingCurves);
                    }

                    return curveGroups;
                }

                // 执行完整合并，确保所有包含和重叠的分组被完全合并
                private List<Extents3d> FullyMergeGroups(List<List<Curve>> initialGroups)
                {
                    List<Extents3d> boundingBoxes = initialGroups.Select(group => CalculateBoundingBox1(group)).ToList();
                    bool hasMerged;

                    do
                    {
                        hasMerged = false;

                        for (int i = 0; i < boundingBoxes.Count; i++)
                        {
                            for (int j = i + 1; j < boundingBoxes.Count; j++)
                            {
                                if (OverlapsOrContains(boundingBoxes[i], boundingBoxes[j]))
                                {
                                    boundingBoxes[i] = MergeBounds1(boundingBoxes[i], boundingBoxes[j]);
                                    boundingBoxes.RemoveAt(j);
                                    hasMerged = true;
                                    break;
                                }
                            }

                            if (hasMerged) break;
                        }
                    } while (hasMerged);

                    return boundingBoxes;
                }

                // 合并两个包围盒
                private Extents3d MergeBounds1(Extents3d box1, Extents3d box2)
                {
                    return new Extents3d(
                        new Point3d(Math.Min(box1.MinPoint.X, box2.MinPoint.X), Math.Min(box1.MinPoint.Y, box2.MinPoint.Y), 0),
                        new Point3d(Math.Max(box1.MaxPoint.X, box2.MaxPoint.X), Math.Max(box1.MaxPoint.Y, box2.MaxPoint.Y), 0)
                    );
                }

                // 扩展包围盒
                private Extents3d ExpandBounds1(Extents3d bounds, double distance)
                {
                    return new Extents3d(
                        new Point3d(bounds.MinPoint.X - distance, bounds.MinPoint.Y - distance, bounds.MinPoint.Z),
                        new Point3d(bounds.MaxPoint.X + distance, bounds.MaxPoint.Y + distance, bounds.MaxPoint.Z)
                    );
                }

                // 计算组的整体包围盒
                private Extents3d CalculateBoundingBox1(List<Curve> curves)
                {
                    Point3d minPoint = new Point3d(double.MaxValue, double.MaxValue, 0);
                    Point3d maxPoint = new Point3d(double.MinValue, double.MinValue, 0);

                    foreach (var curve in curves)
                    {
                        var extents = curve.GeometricExtents;
                        minPoint = new Point3d(
                            Math.Min(minPoint.X, extents.MinPoint.X),
                            Math.Min(minPoint.Y, extents.MinPoint.Y), 0);
                        maxPoint = new Point3d(
                            Math.Max(maxPoint.X, extents.MaxPoint.X),
                            Math.Max(maxPoint.Y, extents.MaxPoint.Y), 0);
                    }

                    return new Extents3d(minPoint, maxPoint);
                }*/

        /*        public void InsertCurvesAndDrawFinalBoundingBoxesAndSaveText(object sender, EventArgs e)
                {
                    // 定义选择区域
                    double xMin = Math.Min(Global.x1, Global.x2);
                    double yMin = Math.Min(Global.y1, Global.y2);
                    double xMax = Math.Max(Global.x1, Global.x2);
                    double yMax = Math.Max(Global.y1, Global.y2);

                    Extents3d selectionRect = new Extents3d(new Point3d(xMin, yMin, 0), new Point3d(xMax, yMax, 0));

                    // 初始化曲线列表和四叉树
                    List<Curve> curves = new List<Curve>();
                    QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 0.5);

                    Database db = HostApplicationServices.WorkingDatabase;

                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                            foreach (ObjectId objId in btr)
                            {
                                Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                                if (entity is Curve curve)
                                {
                                    Extents3d? curveBounds = curve.Bounds;

                                    if (curveBounds.HasValue && IsWithinSelection(curveBounds.Value, selectionRect))
                                    {
                                        curves.Add(curve);
                                        quadTree.AddNode(curve.GeometricExtents.MinPoint, curve); // 插入四叉树
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        // 初步分组
                        List<List<Curve>> initialGroups = GroupCurvesByIntersection(curves, quadTree, 10);

                        // 二次合并重叠的分组
                        List<List<Curve>> finalGroups = MergeOverlappingGroups(initialGroups);

                        // 创建一个字典来存储每个包围盒内的字符
                        Dictionary<Extents3d, List<string>> boundingBoxTextGroups = new Dictionary<Extents3d, List<string>>();

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                            // 遍历所有文本图元
                            foreach (ObjectId objId in btr)
                            {
                                Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                                if (entity is DBText dbText) // 单行文本
                                {
                                    Point3d textPosition = dbText.Position;
                                    foreach (var group in finalGroups)
                                    {
                                        Extents3d boundingBox = CalculateBoundingBox(group);
                                        if (IsPointWithinBoundingBox(textPosition, boundingBox))
                                        {
                                            if (!boundingBoxTextGroups.ContainsKey(boundingBox))
                                            {
                                                boundingBoxTextGroups[boundingBox] = new List<string>();
                                            }
                                            boundingBoxTextGroups[boundingBox].Add(dbText.TextString);
                                        }
                                    }
                                }
                                else if (entity is MText mText) // 多行文本
                                {
                                    Point3d textPosition = mText.Location;
                                    foreach (var group in finalGroups)
                                    {
                                        Extents3d boundingBox = CalculateBoundingBox(group);
                                        if (IsPointWithinBoundingBox(textPosition, boundingBox))
                                        {
                                            if (!boundingBoxTextGroups.ContainsKey(boundingBox))
                                            {
                                                boundingBoxTextGroups[boundingBox] = new List<string>();
                                            }
                                            boundingBoxTextGroups[boundingBox].Add(mText.Contents);
                                        }
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        // 输出到txt文件
                        SaveBoundingBoxTextsToFile(boundingBoxTextGroups, "BoundingBoxTexts.txt");
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show("错误：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }//基于二次分组提取文本（时间2分）

                // 判断文本位置是否在包围盒内
                private bool IsPointWithinBoundingBox(Point3d point, Extents3d boundingBox)
                {
                    return point.X >= boundingBox.MinPoint.X && point.X <= boundingBox.MaxPoint.X &&
                           point.Y >= boundingBox.MinPoint.Y && point.Y <= boundingBox.MaxPoint.Y;
                }

                // 保存分组的文本内容到文件，分组之间空一行
                private void SaveBoundingBoxTextsToFile(Dictionary<Extents3d, List<string>> textGroups, string filePath)
                    {
                        using (StreamWriter writer = new StreamWriter(filePath))
                        {
                            foreach (var kvp in textGroups)
                            {
                                writer.WriteLine($"Bounding Box: Min({kvp.Key.MinPoint.X}, {kvp.Key.MinPoint.Y}), Max({kvp.Key.MaxPoint.X}, {kvp.Key.MaxPoint.Y})");
                                writer.WriteLine("Contained Texts:");
                                foreach (string text in kvp.Value)
                                {
                                    writer.WriteLine(" - " + text);
                                }
                                // 在每个分组之间添加空行
                                writer.WriteLine(); // 空行
                                writer.WriteLine(); // 额外的空行，便于视觉分割
                            }
                        }
                    }*/



        //最新包围盒算法 融合四叉树 二次合并 传递性检测 有瑕疵但无相交包围盒

        // 全局变量，用于存储所有闭合的 Polyline








        public class PolylineWithIndex
        {
            public Polyline Polyline { get; set; }
            public int Index { get; set; }
        }

        private List<PolylineWithIndex> mergedPolylinesWithIndex = new List<PolylineWithIndex>();















        private List<Polyline> mergedPolylines = new List<Polyline>();

        private HashSet<ObjectId> curvesToHide = new HashSet<ObjectId>();
        public void BoundingBoxes(object sender, EventArgs e)
        {
            // 使用Global变量中获取对角点，定义区域
            double xMin = Math.Min(Global.x1, Global.x2);
            double yMin = Math.Min(Global.y1, Global.y2);
            double xMax = Math.Max(Global.x1, Global.x2);
            double yMax = Math.Max(Global.y1, Global.y2);

            // 四叉树边界设置和缓冲距离
            double bufferDistance = 3.0; // 缓冲距离（用户可自定义）
            QuadTreeNode<Curve> quadTree = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 1);

            List<Curve> curves = new List<Curve>();
            Database db = HostApplicationServices.WorkingDatabase;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    // 将区域内的曲线添加到四叉树
                    foreach (ObjectId objId in btr)
                    {
                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity is Curve curve)
                        {
                            Extents3d? curveBounds = curve.Bounds;

                            if (curveBounds.HasValue &&
                                curveBounds.Value.MinPoint.X >= xMin && curveBounds.Value.MaxPoint.X <= xMax &&
                                curveBounds.Value.MinPoint.Y >= yMin && curveBounds.Value.MaxPoint.Y <= yMax)
                            {
                                curves.Add(curve);
                                /*Point3d centerPoint = new Point3d(
                                    (curveBounds.Value.MinPoint.X + curveBounds.Value.MaxPoint.X) / 2,
                                    (curveBounds.Value.MinPoint.Y + curveBounds.Value.MaxPoint.Y) / 2,
                                    0);*/
                                quadTree.AddNode(curve.GeometricExtents.MinPoint, curve);
                            }
                        }
                    }
                    tr.Commit();
                }

                // 使用四叉树进行分组并生成分组的包围盒
                List<List<Curve>> curveGroups = GroupCurvesByIntersection2(curves, quadTree, bufferDistance);

                // 执行完全合并，确保没有相交或包含关系未被合并
                List<Extents3d> fullyMergedBoundingBoxes = PerformCompleteMerging(curveGroups);

                // 绘制合并后的包围盒
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var boundingBox in fullyMergedBoundingBoxes)
                    {


                        Polyline mergedOutline = new Polyline();
                        mergedOutline.AddVertexAt(0, new Point2d(boundingBox.MinPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(1, new Point2d(boundingBox.MaxPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(2, new Point2d(boundingBox.MaxPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(3, new Point2d(boundingBox.MinPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                        mergedOutline.Closed = true;
                        mergedOutline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色

                        // 将合并后的包围盒插入模型空间
                        btr.AppendEntity(mergedOutline);
                        tr.AddNewlyCreatedDBObject(mergedOutline, true);

                        // 将此 Polyline 存储到全局变量
                        mergedPolylines.Add(mergedOutline);
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"错误：{ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        
        public void GroupAndDrawFullyMergedBoundingBoxes(object sender, EventArgs e)
        {
            // 使用Global变量中获取对角点，定义区域
            double xMin = Math.Min(Global.x1, Global.x2);
            double yMin = Math.Min(Global.y1, Global.y2);
            double xMax = Math.Max(Global.x1, Global.x2);
            double yMax = Math.Max(Global.y1, Global.y2);

            // 第一个四叉树：用于插入所有曲线，生成 curvesToHide
            QuadTreeNode<Curve> quadTreeForHide = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 1);

            // 第二个四叉树：用于插入没有被标记为隐藏的曲线，生成包围盒
            double bufferDistance = 3.0;
            QuadTreeNode<Curve> quadTreeForBoundingBoxes = new QuadTreeNode<Curve>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 1);

            List<Curve> curves = new List<Curve>();
            Database db = HostApplicationServices.WorkingDatabase;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    // 清空 curvesToHide 列表
                    curvesToHide.Clear();

                    // 获取用户输入的缓冲区宽度
                    string input = ShowInputDialog("请输入缓冲区宽度：", "缓冲区宽度", "5.0");
                    if (!double.TryParse(input, out double bufferWidth) || bufferWidth <= 0)
                    {
                        MessageBox.Show("无效的宽度值，请输入正数。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 选中 Polyline（假设是全局变量 GlobalEntityData.SelectedEntityId）
                    if (GlobalEntityData.SelectedEntityId == ObjectId.Null)
                    {
                        MessageBox.Show("请先选择一条 Polyline 作为中轴线。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // 加载选中的 Polyline
                    Polyline centralPolyline = tr.GetObject(GlobalEntityData.SelectedEntityId, OpenMode.ForRead) as Polyline;
                    if (centralPolyline == null)
                    {
                        MessageBox.Show("选中的实体不是有效的 Polyline。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // 将模型空间中的所有 Polyline 添加到四叉树（用于生成 curvesToHide）
                    List<Polyline> polylines = new List<Polyline>();
                    foreach (ObjectId objId in btr)
                    {
                        //if (curvesToHide.Contains(objId)) continue;

                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity is Polyline polyline)
                        {
                            polylines.Add(polyline);
                            
                        }
                        if (entity != null && entity is Curve curve)
                        {
                            quadTreeForHide.AddNode(curve.GeometricExtents.MinPoint, curve);
                        }
                    }

                    // 创建队列，用于存储相连的 Polyline 起点（广度优先搜索）
                    Queue<Point3d> findPointQueue = new Queue<Point3d>();
                    findPointQueue.Enqueue(centralPolyline.GetPoint3dAt(0)); // 从选中的 Polyline 起点开始

                    // 用于存储相连 Polyline 的列表
                    List<Polyline> visitedPolylines = new List<Polyline>();

                    // 广度优先搜索，查找与中央 Polyline 相连的 Polyline
                    while (findPointQueue.Count > 0)
                    {
                        Point3d currentPoint = findPointQueue.Dequeue();

                        foreach (Polyline polyline in polylines)
                        {
                            // 如果该 Polyline 尚未访问过，并且起点或终点接近当前点，则视为连接
                            if (!visitedPolylines.Contains(polyline))
                            {
                                if (Math.Abs(currentPoint.X - polyline.GetPoint3dAt(0).X) < 0.1 && Math.Abs(currentPoint.Y - polyline.GetPoint3dAt(0).Y) < 0.1)
                                {
                                    findPointQueue.Enqueue(polyline.GetPoint3dAt(1)); // 将终点加入队列
                                    visitedPolylines.Add(polyline);
                                }
                                else if (Math.Abs(currentPoint.X - polyline.GetPoint3dAt(1).X) < 0.1 && Math.Abs(currentPoint.Y - polyline.GetPoint3dAt(1).Y) < 0.1)
                                {
                                    findPointQueue.Enqueue(polyline.GetPoint3dAt(0)); // 将起点加入队列
                                    visitedPolylines.Add(polyline);
                                }
                            }
                        }
                    }

                    // 处理每个已访问的 Polyline，生成缓冲区并检查相交
                    foreach (Polyline polyline in visitedPolylines)
                    {
                        List<Polyline> straightSegments = BreakPolylineIntoSegmentPolylines(polyline);

                        foreach (Polyline segment in straightSegments)
                        {
                            // 创建缓冲区
                            Polyline offsetBuffer = CreateManualOffsetPolyline(segment, bufferWidth);

                            if (offsetBuffer != null)
                            {
                                // 计算缓冲区的矩形范围
                                Extents3d bounds = offsetBuffer.GeometricExtents;
                                Rect queryBox = new Rect(bounds.MinPoint.X, bounds.MinPoint.Y, bounds.MaxPoint.X - bounds.MinPoint.X, bounds.MaxPoint.Y - bounds.MinPoint.Y);

                                // 查询与缓冲区相交的曲线
                                List<Curve> candidates = new List<Curve>();
                                quadTreeForHide.GetNode(queryBox, ref candidates);

                                // 判断相交
                                foreach (Curve candidate in candidates)
                                {
                                    //if (candidate is Polyline modelPolyline)
                                    {
                                        // 判断是否相交
                                        //if (IsPolylineInsideOrIntersectingRectangle(modelPolyline, offsetBuffer))
                                        {
                                            curvesToHide.Add(candidate.ObjectId); // 将相交的 Polyline 添加到 curvesToHide
                                        }
                                    }
                                }
                            }
                        }
                    }








                    //// 可视化 curvesToHide 中的所有曲线
                    //BlockTableRecord writeBtr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    //// 遍历 curvesToHide 列表，并将其绘制到 AutoCAD 中
                    //foreach (ObjectId objId in curvesToHide)
                    //{
                    //    Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    //    if (entity is Polyline polyline)
                    //    {
                    //        // 创建一个新的 Polyline，并设置其颜色为红色
                    //        Polyline visualizedPolyline = polyline.Clone() as Polyline;
                    //        if (visualizedPolyline != null)
                    //        {
                    //            visualizedPolyline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 红色表示隐藏的曲线

                    //            // 将可视化后的 Polyline 添加到模型空间
                    //            writeBtr.AppendEntity(visualizedPolyline);
                    //            tr.AddNewlyCreatedDBObject(visualizedPolyline, true);
                    //        }
                    //    }
                    //}






                    // 初始化剩余曲线列表
                    List<Curve> remainingCurves = new List<Curve>();

                    // 获取当前块表记录（BlockTableRecord），通常是模型空间或者某个布局空间
                    BlockTableRecord btr1 = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    foreach (ObjectId objId in btr1)
                    {
                        if (curvesToHide.Contains(objId))
                        {
                            continue;
                        }

                        // 获取该 ObjectId 对应的实体
                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;

                        // 判断实体是否为曲线类型
                        if (entity is Curve curve)
                        {
                            // 获取曲线的包围盒
                            Extents3d? curveBounds = curve.Bounds;

                            // 如果包围盒有效并且在定义区域内
                            if (curveBounds.HasValue &&
                                curveBounds.Value.MinPoint.X >= xMin && curveBounds.Value.MaxPoint.X <= xMax &&
                                curveBounds.Value.MinPoint.Y >= yMin && curveBounds.Value.MaxPoint.Y <= yMax)
                            {
                                // 将曲线添加到剩余曲线列表
                                remainingCurves.Add(curve);

                                // 将曲线插入四叉树
                                quadTreeForBoundingBoxes.AddNode(curve.GeometricExtents.MinPoint, curve);
                            }
                        }
                    }

                    // 使用四叉树进行分组并生成分组的包围盒
                    List<List<Curve>> curveGroups = GroupCurvesByIntersection2(remainingCurves, quadTreeForBoundingBoxes, bufferDistance);

                    // 执行完全合并，确保没有相交或包含关系未被合并
                    List<Extents3d> fullyMergedBoundingBoxes = PerformCompleteMerging(curveGroups);

                    // 绘制合并后的包围盒
                    BlockTableRecord writeBtr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (var boundingBox in fullyMergedBoundingBoxes)
                    {
                        Polyline mergedOutline = new Polyline();

                        // 生成包围盒的四个顶点
                        mergedOutline.AddVertexAt(0, new Point2d(boundingBox.MinPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(1, new Point2d(boundingBox.MaxPoint.X, boundingBox.MinPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(2, new Point2d(boundingBox.MaxPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);
                        mergedOutline.AddVertexAt(3, new Point2d(boundingBox.MinPoint.X, boundingBox.MaxPoint.Y), 0, 0, 0);

                        // 完成封闭的多边形
                        mergedOutline.Closed = true;
                        mergedOutline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3); // 绿色

                        // 将合并后的包围盒添加到模型空间
                        writeBtr.AppendEntity(mergedOutline);
                        tr.AddNewlyCreatedDBObject(mergedOutline, true);

                        mergedPolylines.Add(mergedOutline);
                    }

                    // 提交事务
                    tr.Commit();




                    MessageBox.Show("完全合并包围盒生成完成！");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"发生错误：{ex.Message}\n堆栈信息：{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 使用四叉树和缓冲区距离对曲线进行分组
        private List<List<Curve>> GroupCurvesByIntersection2(List<Curve> curves, QuadTreeNode<Curve> quadTree, double bufferDistance)
        {
            List<List<Curve>> curveGroups = new List<List<Curve>>();
            HashSet<Curve> processedCurves = new HashSet<Curve>();

            foreach (var curve in curves)
            {
                if (processedCurves.Contains(curve)) continue;

                Extents3d currentBox = curve.GeometricExtents;
                List<Curve> intersectingCurves = new List<Curve> { curve };
                Queue<Curve> curveQueue = new Queue<Curve>();
                curveQueue.Enqueue(curve);
                processedCurves.Add(curve);

                while (curveQueue.Count > 0)
                {
                    Curve currentCurve = curveQueue.Dequeue();
                    Extents3d box = ExpandBounds(currentCurve.GeometricExtents, bufferDistance);

                    List<Curve> candidates = new List<Curve>();
                    quadTree.GetNode(new Rect(box.MinPoint.X, box.MinPoint.Y, box.MaxPoint.X - box.MinPoint.X, box.MaxPoint.Y - box.MinPoint.Y), ref candidates);

                    foreach (var candidate in candidates)
                    {
                        if (!processedCurves.Contains(candidate) && Overlaps(box, candidate.GeometricExtents))
                        {
                            intersectingCurves.Add(candidate);
                            curveQueue.Enqueue(candidate);
                            processedCurves.Add(candidate);
                        }
                    }
                }

                curveGroups.Add(intersectingCurves);
            }

            return curveGroups;
        }
        // 执行完全合并，确保没有相交或包含关系未被合并   
        private List<Extents3d> PerformCompleteMerging(List<List<Curve>> curveGroups)
        {
            List<Extents3d> boundingBoxes = curveGroups.Select(group => CalculateBoundingBox2(group)).ToList();
            bool hasMerged;

            do
            {
                hasMerged = false;

                for (int i = 0; i < boundingBoxes.Count; i++)
                {
                    for (int j = i + 1; j < boundingBoxes.Count; j++)
                    {
                        if (Overlaps(boundingBoxes[i], boundingBoxes[j]) || Contains(boundingBoxes[i], boundingBoxes[j]) || Contains(boundingBoxes[j], boundingBoxes[i]))
                        {
                            boundingBoxes[i] = MergeBounds(boundingBoxes[i], boundingBoxes[j]);
                            boundingBoxes.RemoveAt(j);
                            hasMerged = true;
                            break;
                        }
                    }

                    if (hasMerged) break;
                }
            } while (hasMerged);

            return boundingBoxes;
        }
        // 计算一组曲线的整体包围盒
        private Extents3d CalculateBoundingBox2(List<Curve> curves)
        {
            List<Point3d> points = curves.SelectMany(curve => new[] { curve.GeometricExtents.MinPoint, curve.GeometricExtents.MaxPoint }).ToList();
            return new Extents3d(
                new Point3d(points.Min(p => p.X), points.Min(p => p.Y), 0),
                new Point3d(points.Max(p => p.X), points.Max(p => p.Y), 0)
            );
        }
        // 合并两个包围盒
        private Extents3d MergeBounds(Extents3d box1, Extents3d box2)
        {
            return new Extents3d(
                new Point3d(Math.Min(box1.MinPoint.X, box2.MinPoint.X), Math.Min(box1.MinPoint.Y, box2.MinPoint.Y), 0),
                new Point3d(Math.Max(box1.MaxPoint.X, box2.MaxPoint.X), Math.Max(box1.MaxPoint.Y, box2.MaxPoint.Y), 0)
            );
        }
        // 扩展包围盒
        private Extents3d ExpandBounds(Extents3d bounds, double distance)
        {
            return new Extents3d(
                new Point3d(bounds.MinPoint.X - distance, bounds.MinPoint.Y - distance, bounds.MinPoint.Z),
                new Point3d(bounds.MaxPoint.X + distance, bounds.MaxPoint.Y + distance, bounds.MaxPoint.Z)
            );
        }
        // 判断包围盒 box1 是否包含 box2
        private bool Contains(Extents3d box1, Extents3d box2)
        {
            return box1.MinPoint.X <= box2.MinPoint.X && box1.MaxPoint.X >= box2.MaxPoint.X &&
                   box1.MinPoint.Y <= box2.MinPoint.Y && box1.MaxPoint.Y >= box2.MaxPoint.Y;
        }
        private bool Overlaps(Extents3d box1, Extents3d box2)
        {
            bool xOverlap = box1.MaxPoint.X >= box2.MinPoint.X && box1.MinPoint.X <= box2.MaxPoint.X;
            bool yOverlap = box1.MaxPoint.Y >= box2.MinPoint.Y && box1.MinPoint.Y <= box2.MaxPoint.Y;
            return xOverlap && yOverlap;
        }


        //点选Polyline进行删除，更新包围盒
        public void toolStripButton_DeletePolyline_Click(object sender, EventArgs e)
        {

            // 提示用户选择 Polyline
            MessageBox.Show("请选择要删除的 Polyline。", "删除 Polyline", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 设置鼠标点击事件来选择 Polyline
            this.basepanel.MouseDown += Basepanel_MouseDown_DeletePolyline;
        }

        private void Basepanel_MouseDown_DeletePolyline(object sender, MouseEventArgs e)
        {
            MouseDownPt = new Point(e.X, e.Y);
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectedEntities = new ObjectIdCollection();

                // 使用鼠标点击位置进行选择
                pView.Select(new Point2dCollection(new Point2d[]
                {
                    new Point2d(MouseDownPt.X + 5, MouseDownPt.Y + 5),
                    new Point2d(MouseDownPt.X - 5, MouseDownPt.Y - 5)
                }),
                new SRnew(selectedEntities, database.CurrentSpaceId), Teigha.GraphicsSystem.SelectionMode.Crossing);

                if (selectedEntities != null && selectedEntities.Count > 0)
                {
                    foreach (ObjectId item in selectedEntities)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead)) // 只读方式打开实体
                            {
                                ent.Highlight();  // 高亮显示选择的实体
                                GlobalEntityData.SelectedEntityId = ent.ObjectId;
                                GlobalEntityData.SelectedEntity = ent;

                                // 判断选中的实体类型是否为 Polyline
                                if (GlobalEntityData.SelectedEntity != null)
                                {
                                    Entity selectedEntity = GlobalEntityData.SelectedEntity;
                                    if (selectedEntity is Polyline polyline)
                                    {
                                        // 提示选择的实体是 Polyline
                                        MessageBox.Show("已选择 Polyline，确认删除。", "选择成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                        // 确认删除 Polyline
                                        DialogResult result = MessageBox.Show("确认删除此 Polyline?", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                                        if (result == DialogResult.Yes)
                                        {
                                            // 开始删除 Polyline
                                            using (Transaction tr = database.TransactionManager.StartTransaction())
                                            {
                                                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);

                                                // 升级 Polyline 为可写模式并删除
                                                polyline.UpgradeOpen();
                                                polyline.Erase();

                                                // 提交事务
                                                tr.Commit();
                                            }

                                            // 提示删除成功
                                            MessageBox.Show("Polyline 已成功删除！", "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                            // 从 mergedPolylines 中删除已删除的 Polyline
                                            mergedPolylines.Remove(polyline);

                                            break; // 结束循环
                                        }
                                    }
                                    else
                                    {
                                        // 如果选择的不是 Polyline，提示重新选择
                                        MessageBox.Show("选择的实体不是 Polyline，请重新选择一个 Polyline。", "选择错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 如果没有选择任何实体，提示重新选择
                    MessageBox.Show("没有选择任何实体，请重新点击选择 Polyline。", "选择错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // 移除事件处理器，避免多次触发
            this.basepanel.MouseDown -= Basepanel_MouseDown_DeletePolyline;
        }



        //线束分组----提取文本
        public void GetTextInsidePolylines(object sender, EventArgs e)
        {
            // 存储所有符合条件的文本图元
            List<Entity> matchingTextEntities = new List<Entity>();

            // 用于存储每个 Polyline 中的文本
            Dictionary<Polyline, List<string>> polylineTextMap = new Dictionary<Polyline, List<string>>();

            // 遍历每个 Polyline
            foreach (Polyline polyline in mergedPolylines)
            {
                // 存储当前 Polyline 包含的文本
                List<string> polylineTexts = new List<string>();

                // 使用 Transaction 来查询文本实体
                using (Transaction tr = database.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(database.CurrentSpaceId, OpenMode.ForRead);

                    // 遍历模型空间中的所有文本实体
                    foreach (ObjectId objId in btr)
                    {
                        Entity entity = (Entity)tr.GetObject(objId, OpenMode.ForRead);

                        // 检查实体是否是文本（DBText 或 MText）
                        if (entity is DBText textEntity)
                        {
                            // 获取文本内容
                            string textContent = textEntity.TextString;

                            // 检查文本是否包含字母和数字的组合
                            if (IsAlphanumericCombination(textContent))
                            {
                                // 判断文本是否在 Polyline 区域内
                                Point3d textPosition = entity.GeometricExtents.MinPoint;

                                if (IsPointInsidePolyline(polyline, textPosition))
                                {
                                    // 将匹配的文本加入到当前 Polyline 的文本列表
                                    polylineTexts.Add(textContent);
                                }
                            }
                        }
                        else if (entity is MText mTextEntity)
                        {
                            // 获取多行文本内容
                            string textContent = mTextEntity.Text;

                            // 检查文本是否包含字母和数字的组合
                            if (IsAlphanumericCombination(textContent))
                            {
                                // 判断文本是否在 Polyline 区域内
                                Point3d textPosition = entity.GeometricExtents.MinPoint;

                                if (IsPointInsidePolyline(polyline, textPosition))
                                {
                                    // 将匹配的文本加入到当前 Polyline 的文本列表
                                    polylineTexts.Add(textContent);
                                }
                            }
                        }
                    }

                    // 提交事务
                    tr.Commit();
                }

                // 如果当前 Polyline 有匹配的文本，将它添加到字典中
                if (polylineTexts.Count > 0)
                {
                    polylineTextMap[polyline] = polylineTexts;
                }
            }

            // 输出匹配的文本到文件
            OutputTextToFile(polylineTextMap);
        }

        // 将匹配的文本输出到文件
        private void OutputTextToFile(Dictionary<Polyline, List<string>> polylineTextMap)
        {
            // 定义输出文件的路径（当前目录下的 output.txt）
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "output.txt");

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var entry in polylineTextMap)
                {
                    Polyline polyline = entry.Key;
                    List<string> texts = entry.Value;

                    // 输出当前 Polyline 对应的文本
                    writer.WriteLine($"Polyline ID: {polyline.Handle.ToString()}");
                    foreach (string text in texts)
                    {
                        writer.WriteLine(text);
                    }

                    // 输出一个空行分隔不同 Polyline 的文本
                    writer.WriteLine();
                }
            }

            MessageBox.Show($"文本输出到文件：{filePath}", "操作成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 检查字符串是否包含字母和数字的组合
        private bool IsAlphanumericCombination(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 用正则表达式检查字母和数字的组合
            var regex = new System.Text.RegularExpressions.Regex(@"^(?=.*[a-zA-Z])(?=.*\d)[a-zA-Z\d]+$");
            return regex.IsMatch(text);
        }

        // 判断点是否在多边形内的函数
        private bool IsPointInsidePolyline(Polyline polyline, Point3d point)
        {
            // 获取 Polyline 的所有顶点
            List<Point2d> vertices = new List<Point2d>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                vertices.Add(polyline.GetPoint2dAt(i));
            }

            // 使用射线法判断点是否在多边形内
            return IsPointInPolygon(vertices, new Point2d(point.X, point.Y));
        }

        // 射线法：判断点是否在多边形内
        private bool IsPointInPolygon(List<Point2d> polygonVertices, Point2d testPoint)
        {
            int n = polygonVertices.Count;
            bool result = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                // 获取当前边的起始点和结束点
                Point2d vertex1 = polygonVertices[i];
                Point2d vertex2 = polygonVertices[j];

                // 判断点是否在线段的左侧或右侧
                if (((vertex1.Y > testPoint.Y) != (vertex2.Y > testPoint.Y)) &&
                    (testPoint.X < (vertex2.X - vertex1.X) * (testPoint.Y - vertex1.Y) / (vertex2.Y - vertex1.Y) + vertex1.X))
                {
                    result = !result;
                }
            }
            return result;
        }




        //字符分组----提取闭合Polyline的文本
        /*        public void ExportTextInsidePolylinesToTxt(object sender, EventArgs e)
                {
                    // 创建一个存储每个 Polyline 内部文本内容的列表
                    List<List<string>> polylineTexts = new List<List<string>>();

                    using (Transaction tr = database.TransactionManager.StartTransaction())
                    {
                        // 获取模型空间中的图元
                        BlockTable blockTable = tr.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                        List<Entity> nonChineseTextEntities = new List<Entity>();
                        List<Polyline> closedPolylines = new List<Polyline>();

                        // 遍历模型空间中的图元
                        foreach (ObjectId objId in modelSpace)
                        {
                            Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;

                            // 筛选非汉字文本
                            if (entity is DBText dbText && !ContainsChineseCharacters(dbText.TextString))
                            {
                                nonChineseTextEntities.Add(entity);
                            }
                            else if (entity is MText mText && !ContainsChineseCharacters(mText.Text))
                            {
                                nonChineseTextEntities.Add(entity);
                            }
                            else if (entity is Polyline polyline && polyline.Closed)
                            {
                                closedPolylines.Add(polyline);
                            }
                        }

                        // 对每个封闭的 Polyline，收集其内部的文本内容
                        foreach (Polyline polyline in closedPolylines)
                        {
                            List<string> textsInPolyline = new List<string>();

                            foreach (Entity textEntity in nonChineseTextEntities)
                            {
                                Point3d textPosition = GetTextPosition(textEntity);

                                // 判断文本是否在 Polyline 内部
                                if (IsPointInsidePolyline1(polyline, textPosition))
                                {
                                    textsInPolyline.Add(GetTextContent(textEntity));
                                }
                            }

                            // 将当前 Polyline 内的文本添加到结果列表中
                            if (textsInPolyline.Count > 0)
                            {
                                polylineTexts.Add(textsInPolyline);
                            }
                        }

                        tr.Commit();
                    }

                    // 输出到文本文件
                    string filePath = Path.Combine(Environment.CurrentDirectory, "PolylineTexts.txt");
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        foreach (List<string> texts in polylineTexts)
                        {
                            foreach (string text in texts)
                            {
                                writer.WriteLine(text);
                            }
                            writer.WriteLine(); // 空行区分不同 Polyline 的文本内容
                        }
                    }

                    Console.WriteLine($"文本已输出到 {filePath}");
                }

                // 判断文本内容是否包含汉字的辅助方法
                private bool ContainsChineseCharacters(string text)
                {
                    foreach (char c in text)
                    {
                        if (c >= 0x4E00 && c <= 0x9FFF) // 汉字的Unicode范围
                        {
                            return true;
                        }
                    }
                    return false;
                }

                // 获取文本位置的辅助方法
                private Point3d GetTextPosition(Entity textEntity)
                {
                    if (textEntity is DBText dbText)
                    {
                        return dbText.Position;
                    }
                    else if (textEntity is MText mText)
                    {
                        return mText.Location;
                    }
                    return Point3d.Origin;
                }

                // 判断点是否在 Polyline 内部的辅助方法
                private bool IsPointInsidePolyline1(Polyline polyline, Point3d point)
                {
                    // 获取 Polyline 的所有顶点
                    List<Point2d> vertices = new List<Point2d>();
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        vertices.Add(polyline.GetPoint2dAt(i));
                    }

                    // 使用射线法判断点是否在多边形内
                    return IsPointInPolygon1(vertices, new Point2d(point.X, point.Y));
                }

                // 射线法：判断点是否在多边形内
                private bool IsPointInPolygon1(List<Point2d> polygonVertices, Point2d testPoint)
                {
                    int n = polygonVertices.Count;
                    bool result = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        Point2d vertex1 = polygonVertices[i];
                        Point2d vertex2 = polygonVertices[j];

                        if (((vertex1.Y > testPoint.Y) != (vertex2.Y > testPoint.Y)) &&
                            (testPoint.X < (vertex2.X - vertex1.X) * (testPoint.Y - vertex1.Y) / (vertex2.Y - vertex1.Y) + vertex1.X))
                        {
                            result = !result;
                        }
                    }
                    return result;
                }

                // 获取文本内容的辅助方法
                private string GetTextContent(Entity textEntity)
                {
                    if (textEntity is DBText dbText)
                    {
                        return dbText.TextString;
                    }
                    else if (textEntity is MText mText)
                    {
                        return mText.Text;
                    }
                    return string.Empty;
                }*/


        public void OpencvForContour(object sender, EventArgs e)
        {



            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();












            // 获取当前数据库
            Database db = HostApplicationServices.WorkingDatabase;

            try
            {
                // 从Global变量中获取选择框坐标
                double x1 = Global.x1;
                double y1 = Global.y1;
                double x2 = Global.x2;
                double y2 = Global.y2;

                // 计算选择框的边界
                double xMin = Math.Min(x1, x2);
                double yMin = Math.Min(y1, y2);
                double xMax = Math.Max(x1, x2);
                double yMax = Math.Max(y1, y2);

                //Console.WriteLine($"选择区域: ({xMin},{yMin}) 到 ({xMax},{yMax})");

                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 创建选择窗口的点
                    Point3d minPoint = new Point3d(xMin, yMin, 0);
                    Point3d maxPoint = new Point3d(xMax, yMax, 0);

                    // 获取选择窗口内的实体
                    List<Entity> ents = new List<Entity>();
                    Point3dCollection points = new Point3dCollection
                    {
                        minPoint,
                        new Point3d(maxPoint.X, minPoint.Y, 0),
                        maxPoint,
                        new Point3d(minPoint.X, maxPoint.Y, 0)
                    };

                    BlockTable blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    //foreach (ObjectId objectId in modelSpace)
                    //{
                    //    DBObject obj = trans.GetObject(objectId, OpenMode.ForRead);

                    //    if (obj is Entity entity)
                    //    {
                    //        Extents3d extents = entity.GeometricExtents;
                    //        if (extents.MinPoint.X >= minPoint.X && extents.MaxPoint.X <= maxPoint.X &&
                    //            extents.MinPoint.Y >= minPoint.Y && extents.MaxPoint.Y <= maxPoint.Y)
                    //        {
                    //            ents.Add(entity);
                    //        }
                    //    }
                    //}


                    foreach (ObjectId objectId in modelSpace)
                    {
                        DBObject obj = trans.GetObject(objectId, OpenMode.ForRead);

                        if (obj is Entity entity && !entity.IsErased && entity.ObjectId.IsValid)
                        {
                            if (entity is DBText dbText)
                            {
                                // 检查文本高度和字符串
                                if (dbText.Height > 0 && !string.IsNullOrEmpty(dbText.TextString))
                                {
                                    try
                                    {
                                        Extents3d extents = dbText.GeometricExtents;
                                        if (extents.MinPoint.X >= minPoint.X && extents.MaxPoint.X <= maxPoint.X &&
                                            extents.MinPoint.Y >= minPoint.Y && extents.MaxPoint.Y <= maxPoint.Y)
                                        {
                                            ents.Add(entity);
                                        }
                                    }
                                    catch (Teigha.Runtime.Exception ex)
                                    {
                                        //Console.WriteLine($"Failed to get extents for DBText, ObjectId: {entity.ObjectId}. Error: {ex.Message}");
                                        continue;
                                    }
                                }
                                else
                                {
                                    //Console.WriteLine($"Skipping invalid DBText, ObjectId: {entity.ObjectId}, Height: {dbText.Height}, Text: {dbText.TextString}");
                                    continue;
                                }
                            }
                            else
                            {
                                // 处理其他实体类型
                                try
                                {
                                    Extents3d extents = entity.GeometricExtents;
                                    if (extents.MinPoint.X >= minPoint.X && extents.MaxPoint.X <= maxPoint.X &&
                                        extents.MinPoint.Y >= minPoint.Y && extents.MaxPoint.Y <= maxPoint.Y)
                                    {
                                        ents.Add(entity);
                                    }
                                }
                                catch (Teigha.Runtime.Exception ex)
                                {
                                    //Console.WriteLine($"Failed to get extents for entity {entity.GetType().Name}, ObjectId: {entity.ObjectId}. Error: {ex.Message}");
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            //Console.WriteLine($"Skipping invalid entity, ObjectId: {objectId}");
                        }
                    }

                    Console.WriteLine($"选择区域内找到 {ents.Count} 个实体");

                    if (ents.Count == 0)
                    {
                        //Console.WriteLine("选择区域内没有实体，操作终止");
                        return;
                    }

                    // 计算所有实体的几何范围
                    List<Extents3d> extensions = ents.Select(x => x.GeometricExtents).ToList();
                    List<Point3d> points_list = new List<Point3d>();
                    foreach (Extents3d item in extensions)
                    {
                        Point3d p1 = item.MinPoint;
                        Point3d p2 = item.MaxPoint;
                        points_list.Add(p1);
                        points_list.Add(p2);
                    }

                    // 计算整体边界范围
                    List<double> p_x = points_list.Select(x => x.X).ToList();
                    List<double> p_y = points_list.Select(y => y.Y).ToList();
                    var boundaryXMin = (int)(p_x.OrderBy(x => x).First());
                    var boundaryXMax = (int)(p_x.OrderByDescending(x => x).First());
                    var boundaryYMin = (int)(p_y.OrderBy(x => x).First());
                    var boundaryYMax = (int)(p_y.OrderByDescending(x => x).First());
                    var minP = new Point3d(boundaryXMin, boundaryYMin, 0);
                    var maxP = new Point3d(boundaryXMax, boundaryYMax, 0);
                    //Console.WriteLine($"实体整体范围: ({boundaryXMin},{boundaryYMin}) 到 ({boundaryXMax},{boundaryYMax})");

                    // 计算宽高和参考点
                    int width = (int)(maxP.X) - (int)(minP.X);
                    int height = (int)(maxP.Y) - (int)(minP.Y);
                    var topLeftPoint = new Point3d(minP.X, minP.Y + height, 0);
                    var bottomRightPoint = new Point3d(maxP.X, maxP.Y - height, 0);
                    //Console.WriteLine($"处理区域宽高: {width} x {height}");

                    // 分辨率增强因子 - 用于提高图像精度
                    int scaleFactor = 2;

                    // 优化采样步长：确保图像中点间距约为1像素
                    double step = 1.0 / scaleFactor;

                    // 准备点集合
                    List<Point3d> outlines = new List<Point3d>();
                    List<Point3d> outlinesModified = new List<Point3d>();
                    List<List<OpenCvSharp.Point>> curvesImagePoints = new List<List<OpenCvSharp.Point>>();

                    // 处理每个实体，将曲线转换为点集
                    //Console.WriteLine("开始处理实体并采样曲线点...");
                    foreach (Entity entity in ents)
                    {
                        if (entity is Curve curve)
                        {
                            List<OpenCvSharp.Point> curveImagePoints = new List<OpenCvSharp.Point>();
                            try
                            {
                                // 获取曲线长度
                                double endParam = curve.EndParam;
                                double curveLength = curve.GetDistanceAtParameter(endParam);


                                if (curveLength <= 0)
                                {
                                    continue; // 跳过长度为0的曲线
                                }

                                // 优化采样：沿曲线等距采样
                                List<Point3d> curvePoints = new List<Point3d>();
                                double dist = 0;
                                while (dist <= curveLength)
                                {
                                    var p = curve.GetPointAtDist(dist);
                                    curvePoints.Add(p);
                                    dist += step;
                                }
                                // 确保包含终点
                                if (dist - step < curveLength)
                                {
                                    var endP = curve.GetPointAtParameter(curve.EndParam);
                                    curvePoints.Add(endP);
                                }

                                // 转换到图像坐标
                                foreach (var p in curvePoints)
                                {
                                    var px = (int)((p.X - minP.X) * scaleFactor);
                                    var py = (int)((-p.Y + maxP.Y) * scaleFactor);
                                    curveImagePoints.Add(new OpenCvSharp.Point(px, py));
                                    outlines.Add(p);
                                    outlinesModified.Add(new Point3d(p.X - topLeftPoint.X, -p.Y + topLeftPoint.Y, 0));
                                }
                                curvesImagePoints.Add(curveImagePoints);
                            }
                            catch (System.Exception ex)
                            {
                                //Console.WriteLine($"处理曲线时出错: {ex.Message}");
                            }
                        }
                    }
                    //Console.WriteLine($"完成曲线采样，共生成 {curvesImagePoints.Sum(x => x.Count)} 个采样点");

                    // 创建高分辨率图像
                    Mat image = new Mat(new OpenCvSharp.Size(width * scaleFactor, height * scaleFactor), MatType.CV_8UC3, new Scalar(255, 255, 255));
                    //Console.WriteLine($"创建图像，尺寸: {width * scaleFactor} x {height * scaleFactor}");

                    // 在图像上绘制每条曲线的线条
                    foreach (var curvePoints in curvesImagePoints)
                    {
                        for (int i = 0; i < curvePoints.Count - 1; i++)
                        {
                            Cv2.Line(image, curvePoints[i], curvePoints[i + 1], new Scalar(0, 0, 0), 1);
                        }
                    }

                    // 图像处理: 转为灰度并二值化
                    //Console.WriteLine("开始图像处理...");
                    Mat imageGray = new Mat();
                    Cv2.CvtColor(image, imageGray, ColorConversionCodes.BGR2GRAY);
                    Mat binaryImage = new Mat();
                    Cv2.Threshold(imageGray, binaryImage, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

                    // 优化：应用形态学闭运算以连接小间隙
                    var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
                    Cv2.MorphologyEx(binaryImage, binaryImage, MorphTypes.Close, kernel);

                    // 轮廓检测
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(binaryImage, out contours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
                    //Console.WriteLine($"初始轮廓检测完成，找到 {contours.Length} 个轮廓");

                    // 筛选外部轮廓并设置面积阈值
                    List<OpenCvSharp.Point[]> externalContours = new List<OpenCvSharp.Point[]>();
                    double minArea = 10.0 * scaleFactor * scaleFactor;
                    for (int i = 0; i < contours.Length; i++)
                    {
                        if (hierarchy[i].Parent == -1)
                        {
                            double area = Cv2.ContourArea(contours[i]);
                            if (area > minArea)
                            {
                                externalContours.Add(contours[i]);
                            }
                        }
                    }
                    //Console.WriteLine($"筛选后保留 {externalContours.Count} 个外部轮廓");

                    // 在图像上绘制轮廓
                    Cv2.DrawContours(image, externalContours, -1, new Scalar(0, 0, 255), 2);

                    // 创建填充的轮廓图像
                    Mat filledImage = image.Clone();
                    for (int i = 0; i < externalContours.Count; i++)
                    {
                        Cv2.FillPoly(
                            filledImage,
                            new[] { externalContours[i] },
                            new Scalar(0, 0, 255),
                            LineTypes.Link8
                        );
                    }

                    // 执行膨胀操作，增强轮廓
                    //Console.WriteLine("执行形态学处理增强轮廓...");
                    Mat dilatedImage = new Mat();
                    var dilateElement = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(7, 7));
                    Cv2.Dilate(filledImage, dilatedImage, dilateElement, null, 2);




                    //执行侵蚀操作
                    Mat erodedImage = new Mat();
                    var erodeElement = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(6, 6));
                    Cv2.Erode(dilatedImage, erodedImage, erodeElement, null, 2);





                    // 转换为灰度图
                    Mat dilatedGray = new Mat();
                    Cv2.CvtColor(erodedImage, dilatedGray, ColorConversionCodes.BGR2GRAY);

                    // 再次二值化处理
                    Mat dilatedBinary = new Mat();
                    Cv2.Threshold(dilatedGray, dilatedBinary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                    // 检查图像统计信息，判断是否需要反转
                    Scalar meanVal = Cv2.Mean(dilatedBinary);
                    //Console.WriteLine($"二值图像平均值: {meanVal[0]}");

                    // 如果平均值较高（大部分是白色），需要反转二值图
                    Mat workingBinary = new Mat();
                    if (meanVal[0] > 127)
                    {
                        //Console.WriteLine("二值图主要是白色，执行反转操作");
                        Cv2.BitwiseNot(dilatedBinary, workingBinary);
                    }
                    else
                    {
                        workingBinary = dilatedBinary.Clone();
                    }

                    // 使用EXTERNAL模式检测最终轮廓
                    //Console.WriteLine("开始最终轮廓检测...");
                    OpenCvSharp.Point[][] dilatedContours;
                    HierarchyIndex[] dilatedHierarchy;
                    Cv2.FindContours(workingBinary, out dilatedContours, out dilatedHierarchy,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                    //Console.WriteLine($"最终检测到 {(dilatedContours != null ? dilatedContours.Length : 0)} 个轮廓");

                    // 面积筛选
                    List<OpenCvSharp.Point[]> filteredContours = new List<OpenCvSharp.Point[]>();
                    double minArea1 = 300;

                    foreach (var contour in dilatedContours)
                    {
                        if (contour.Length > 2)
                        {
                            double area = Cv2.ContourArea(contour);
                            //Console.WriteLine($"轮廓点数: {contour.Length}, 面积: {area}");

                            if (area >= minArea1)
                            {
                                filteredContours.Add(contour);
                            }
                        }
                    }

                    //Console.WriteLine($"面积筛选后保留 {filteredContours.Count} 个轮廓");

                    // 使用筛选后的轮廓
                    List<OpenCvSharp.Point[]> finalContours;
                    if (filteredContours.Count > 0)
                    {
                        finalContours = filteredContours;
                        //Console.WriteLine("使用面积筛选后的轮廓");
                    }
                    else
                    {
                        finalContours = externalContours;
                        //Console.WriteLine("警告：筛选后没有保留任何轮廓，使用初始外部轮廓");
                    }

                    // 轮廓多边形近似 - 使用较低的精度保留更多细节
                    //Console.WriteLine("开始轮廓多边形近似...");
                    List<OpenCvSharp.Point[]> contoursModified = new List<OpenCvSharp.Point[]>();
                    foreach (var contour in finalContours)
                    {
                        var approxCurve = new Mat();
                        var inputContour = InputArray.Create(contour);
                        Cv2.ApproxPolyDP(inputContour, approxCurve, 1, true);
                        OpenCvSharp.Point[] points_;
                        approxCurve.GetArray(out points_);
                        contoursModified.Add(points_);
                    }

                    // 转换为CAD坐标系
                    //Console.WriteLine("转换轮廓到CAD坐标系...");
                    List<List<Point3d>> contours_cad = new List<List<Point3d>>();
                    foreach (var contour in contoursModified)
                    {
                        List<Point3d> contour_cad = new List<Point3d>();
                        foreach (var point in contour)
                        {
                            Point3d point3d = new Point3d(
                            point.X / scaleFactor + topLeftPoint.X,
                            -point.Y / scaleFactor + topLeftPoint.Y,
                            0
                        );
                            contour_cad.Add(point3d);
                        }
                        if (contour_cad.Count >= 3)
                        {
                            //Console.WriteLine($"CAD轮廓点数: {contour_cad.Count}");
                            contours_cad.Add(contour_cad);
                        }
                    }

                    // 在CAD中绘制轮廓线
                    //Console.WriteLine("在CAD中创建多段线...");
                    List<Polyline> polylines = new List<Polyline>();
                    BlockTable blockTableWrite = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord modelSpaceWrite = (BlockTableRecord)trans.GetObject(blockTableWrite[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var contour_cad in contours_cad)
                    {
                        Polyline polyline = new Polyline();
                        for (int i = 0; i < contour_cad.Count; i++)
                        {
                            polyline.AddVertexAt(i, new Point2d(contour_cad[i].X, contour_cad[i].Y), 0, 0, 0);
                        }
                        polyline.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);
                        polyline.Closed = true;
                        //polylines.Add(polyline);
                        //mergedPolylines.Add(polyline);

                        modelSpaceWrite.AppendEntity(polyline);
                        trans.AddNewlyCreatedDBObject(polyline, true);




                        // 创建一个新的PolylineWithIndex对象，并为其分配序号
                        PolylineWithIndex polylineWithIndex = new PolylineWithIndex
                        {
                            Polyline = polyline,
                            Index = mergedPolylinesWithIndex.Count + 1  // 按顺序分配序号
                        };

                        // 将此 PolylineWithIndex 存储到全局变量
                        mergedPolylinesWithIndex.Add(polylineWithIndex);



                    }

                    //Console.WriteLine($"成功创建 {polylines.Count} 个轮廓多段线");
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                string msg = "错误: " + ex.Message + "\n" + "位置: " + ex.StackTrace;
                MessageBox.Show(msg, "轮廓检测错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }









            stopwatch.Stop();
            Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");










        }


        double leftx = 270.151;//请主动输入 
        double rightx = 2879.2266;//矩形对角点
        double portmax = 160;//最大端口号
        double txt_heighe = 2.7344;//接插件字体高度
        Point3d startpoint = new Point3d(605.651, 420.2656, 0);//点选Polyline的起点
        double quanzhi_height = 2.7344;//主干线线长字体高度
        double wei_dis = 1.8;//第一二张图1.8 这个是自定义的，用来判断线长文本到主干线的距离

        public void toxianshuToolStripMenuItem_Click(object sender, EventArgs e)//重点修改
        {





            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start(); // 开始计时











            //这是时间处理函数，点击toxianshuToolStripMenuItem时触发
            try
            {
                // 尝试执行以下代码块，如果发生异常则捕获并处理
                // 获取当前数据库的事务管理器
                TransactionManager tm = database.TransactionManager;
                // 开始一个新的事务
                using (Transaction tr = tm.StartTransaction())
                {
                    // 使用using语句确保资源的正确释放，获取当前空间的块表记录
                    using (BlockTableRecord modelSpace = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForWrite))
                    {






                        //Stopwatch stopwatch = new Stopwatch();
                        //stopwatch.Start(); // 开始计时





                        //第四张图读poly出错
                        #region 读取所有图元(设置是否为多段线）


                        Stopwatch stopwatch1 = new Stopwatch();
                        stopwatch1.Start(); // 开始计时


                        List<Arc> arcs = new List<Arc>();
                        List<DBText> txts = new List<DBText>();
                        List<MText> mtxts = new List<MText>();
                        List<Circle> cirs = new List<Circle>();
                        List<Circle> cirbx = new List<Circle>();
                        List<Line> lines = new List<Line>();

                        List<Point3d> bxs = new List<Point3d>();
                        List<Point3d> bxe = new List<Point3d>();
                        List<Point3d> jdq1 = new List<Point3d>();
                        List<Line> jdqr = new List<Line>();
                        List<Point3d> jdqkg = new List<Point3d>();
                        List<Point3d> jdqbx = new List<Point3d>();
                        List<DBText> jdqtxt = new List<DBText>();//保险丝信息

                        List<Teigha.DatabaseServices.Polyline> Polylines = new List<Teigha.DatabaseServices.Polyline>();
                        List<Poly> Polys = new List<Poly>();
                        List<my_line> my_lines = new List<my_line>();
                        List<Point3d> Hatchs = new List<Point3d>();
                        File.Delete("ttp.txt");
                        
                        foreach (ObjectId oid in modelSpace)
                        {
                            DBObject dbobj = tr.GetObject(oid, OpenMode.ForWrite);
                            if (dbobj is Entity)
                            {

                                Entity entity = dbobj as Entity;
                                string enttype = entity.GetRXClass().Name;
                                entity.Unhighlight();
                                //File.AppendAllText("ttp.txt", enttype + "\r\n"/*, Encoding.Default*/);
                                if (enttype == "AcDbMText")
                                {
                                    MText acMText = entity as MText;
                                    mtxts.Add(acMText);
                                }
                                if (enttype == "AcDbArc")
                                {
                                    Arc acArc = entity as Arc;
                                    arcs.Add(acArc);
                                }
                                if (enttype == "AcDbText")
                                {
                                    DBText acText = entity as DBText;
                                    txts.Add(acText);
                                }
                                if (enttype == "AcDbCircle")
                                {
                                    Circle acCircle = entity as Circle;
                                    cirs.Add(acCircle);
                                }
                                if (enttype == "AcDbLine")
                                {

                                    Line acLine = entity as Line;
                                    if (acLine.Length > 5)
                                    {
                                        lines.Add(acLine);
                                        my_line p1;
                                        p1.line = acLine;
                                        p1.visit = false;
                                        my_lines.Add(p1);
                                    }

                                }
                                if (enttype == "AcDbPolyline")
                                {
                                    Polyline acPolyline = entity as Polyline;//筛选多段线段长度
                                    //bool isPolylineClosed = true;//选择末端直线时判断

                                    //if (!isPolylineClosed)//第一张图不能加
                                    
                                        //Console.WriteLine(acPolyline.GetLineSegmentAt(0).Length); 
                                        //if (acPolyline.GetLineSegmentAt(0).Length > 19)//第一张图里为19，后面有的是6
                                        
                                            Poly pl;
                                            pl.line = acPolyline;
                                            pl.visit = false;
                                            Polys.Add(pl);
                                            Polylines.Add(acPolyline);
                                        
                                    
                                }
                                if (enttype == "AcDbHatch")
                                {

                                    Hatch achatch = entity as Hatch;
                                    if (achatch.NumberOfLoops == 1)
                                    {
                                        var stretchPoints = new Point3dCollection();
                                        achatch.GetStretchPoints(stretchPoints);
                                        foreach (Point3d point in stretchPoints)
                                        {
                                            Hatchs.Add(point);
                                            // 处理拉伸点的坐标
                                            //double x = point.X;
                                            //double y = point.Y;
                                            //File.AppendAllText("ttp.txt", x + " " + y + "\r\n"/*, Encoding.Default*/);
                                        }
                                    }


                                    //File.AppendAllText("ttp.txt", achatch.NumberOfHatchLines+" "+achatch.NumberOfLoops+"\r\n"/*, Encoding.Default*/);

                                }

                            }
                        }



                        

                        //读取图元 
                        #endregion

                        #region 计算点线距离函数
                        Point3d CalculateProjection(Point3d A, Point3d B, Point3d P)
                        {
                            // 计算向量AB和向量AP
                            double ABx = B.X - A.X;
                            double ABy = B.Y - A.Y;
                            double APx = P.X - A.X;
                            double APy = P.Y - A.Y;

                            // 计算向量AP和向量AB的点积
                            double dotProduct = APx * ABx + APy * ABy;

                            // 计算向量AB的模长的平方
                            double ABLengthSquared = ABx * ABx + ABy * ABy;

                            // 计算投影点的坐标
                            double t = dotProduct / ABLengthSquared;
                            double projectionX = A.X + ABx * t;
                            double projectionY = A.Y + ABy * t;

                            return new Point3d(projectionX, projectionY, 0);
                        }
                        double DistancePointToLineSegment(Point3d point, Point3d lineStart, Point3d lineEnd)
                        {
                            double dx = lineEnd.X - lineStart.X;
                            double dy = lineEnd.Y - lineStart.Y;

                            if ((dx == 0) && (dy == 0))
                            {
                                // The line segment is actually a point.
                                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                            }

                            // Calculate the t that minimizes the distance.
                            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

                            // See if this represents one of the segment's endpoints or a point in the middle.
                            if (t < 0)
                            {
                                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                            }
                            else if (t > 1)
                            {
                                return Math.Sqrt(Math.Pow(point.X - lineEnd.X, 2) + Math.Pow(point.Y - lineEnd.Y, 2));
                            }
                            else
                            {
                                Point3d closest = new Point3d(lineStart.X + t * dx, lineStart.Y + t * dy, 0);
                                return Math.Sqrt(Math.Pow(point.X - closest.X, 2) + Math.Pow(point.Y - closest.Y, 2));
                            }
                        }
                        #endregion

                        #region 初始设置
                        if (GlobaltextData1.SelectedEntityId != ObjectId.Null)
                        {
                            using (Entity ent = (Entity)GlobaltextData1.SelectedEntityId.GetObject(OpenMode.ForRead))
                            {
                                if (ent != null)
                                {
                                    Type entityType = GlobaltextData1.SelectedEntity.GetType();
                                    if (ent is DBText tbt)
                                    {
                                        quanzhi_height = tbt.Height;

                                    }
                                }
                                else
                                {
                                    MessageBox.Show("权值为空", "提示");
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("未选中权值", "提示");
                        }
                        if (GlobaltextData2.SelectedEntityId != ObjectId.Null)
                        {
                            using (Entity ent = (Entity)GlobaltextData2.SelectedEntityId.GetObject(OpenMode.ForRead))
                            {
                                if (ent != null)
                                {
                                    Type entityType = GlobaltextData2.SelectedEntity.GetType();
                                    if (ent is DBText tbt)
                                    {

                                        txt_heighe = tbt.Height;

                                    }
                                }
                                else
                                {
                                    MessageBox.Show("接插件为空", "提示");
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("未选中接插件", "提示");
                        }
                        bool lop = false;
                        if (GlobalEntityData.SelectedEntityId != ObjectId.Null )
                        {
                            using (Entity ent = (Entity)GlobalEntityData.SelectedEntityId.GetObject(OpenMode.ForRead))
                            {
                                if (ent != null)
                                {
                                    Type entityType = GlobalEntityData.SelectedEntityId.GetType();

                                    if (ent is Line line)
                                    {
                                        //lineEntity = line;
                                        Point3d startPoint = line.StartPoint;
                                        Point3d endPoint = line.EndPoint;
                                        startpoint = line.StartPoint;
                                        lop = true;
                                        //MessageBox.Show(startPoint.ToString(), "起点");
                                        //MessageBox.Show(endPoint.ToString(), "终点");
                                    }
                                    else if (ent is Polyline pline)
                                    {
                                        startpoint = pline.GetPoint3dAt(0);
                                        lop = false;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show("未选中初始线", "提示");
                                }
                            }
                        }
                        leftx = Global.mlist.First();
                        rightx = Global.mlist[2];
                        #endregion
                        double form_leftx = leftx;//请主动输入
                        double form_rightx = rightx;
                        //过滤表格中的数据
                        #region 得到表结构的端口四属性

                        List<DBText> cav_txt = new List<DBText>();
                        List<DBText> No_txt = new List<DBText>();
                        List<DBText> col_txt = new List<DBText>();
                        List<DBText> spec_txt = new List<DBText>();
                        List<DBText> rest_txt = new List<DBText>();
                        foreach (DBText i in txts)
                        {
                            if (form_leftx < i.Position.X && i.Position.X < form_rightx)
                            {
                                if (i.TextString.Length >= 3)
                                {
                                    if (i.TextString.Substring(0, 3).ToLower() == "cav")
                                    {
                                        cav_txt.Add(i);
                                        continue;
                                    }
                                    if (i.TextString.Substring(0, 2).ToLower() == "no")
                                    {
                                        No_txt.Add(i);
                                        continue;
                                    }
                                    if (i.TextString.Substring(0, 3).ToLower() == "col")
                                    {
                                        col_txt.Add(i);
                                        continue;
                                    }
                                    if (i.TextString.Substring(0, 3).ToLower() == "csa")
                                    {
                                        spec_txt.Add(i);
                                        continue;
                                    }

                                }
                                if (i.TextString.Length >= 4)
                                {
                                    if (i.TextString.Substring(0, 4).ToLower() == "spec")
                                    {
                                        spec_txt.Add(i);
                                        continue;
                                    }

                                }

                                rest_txt.Add(i);
                            }

                        }
                        txts = rest_txt;
                        List<DBText> widtxt1 = new List<DBText>();
                        List<DBText> porttxt1 = new List<DBText>();
                        List<DBText> ltxt1 = new List<DBText>();
                        List<List<DBText>> xstxt1 = new List<List<DBText>>();
                        Regex chineseRegex = new Regex(@"^[\u4e00-\u9fa5]");
                        Regex letterRegex = new Regex(@"^S");
                        foreach (DBText i in No_txt)
                        {
                            Point3d cav_pos = i.Position;
                            List<DBText> lins = new List<DBText>();
                            for (int j = 0; j < txts.Count; j++)
                            {
                                // 检查文本是否以汉字或字母开头
                                bool startsWithChinese = chineseRegex.IsMatch(txts[j].TextString);
                                bool startsWithS = letterRegex.IsMatch(txts[j].TextString);

                                if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f && !startsWithChinese)
                                {
                                    lins.Add(txts[j]);
                                    cav_pos = txts[j].Position;
                                    txts.RemoveAt(j);
                                    j = 0;
                                }
                            }
                            xstxt1.Add(lins);
                        }
                        foreach (DBText i in cav_txt)
                        {
                            Point3d cav_pos = i.Position;
                            for (int j = 0; j < txts.Count; j++)
                            {
                                bool startsWithChinese = chineseRegex.IsMatch(txts[j].TextString);
                                bool startsWithS = letterRegex.IsMatch(txts[j].TextString);
                                if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f && !startsWithChinese && !startsWithS)
                                {
                                    porttxt1.Add(txts[j]);
                                    cav_pos = txts[j].Position;
                                    txts.RemoveAt(j);
                                    j = 0;
                                }

                            }
                        }
                        foreach (DBText i in col_txt)
                        {
                            Point3d cav_pos = i.Position;
                            for (int j = 0; j < txts.Count; j++)
                            {
                                bool startsWithChinese = chineseRegex.IsMatch(txts[j].TextString);
                                bool startsWithS = letterRegex.IsMatch(txts[j].TextString);
                                if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f && !startsWithChinese && !startsWithS)
                                {
                                    ltxt1.Add(txts[j]);
                                    cav_pos = txts[j].Position;
                                    txts.RemoveAt(j);
                                    j = 0;
                                }

                            }
                        }
                        foreach (DBText i in spec_txt)
                        {
                            Point3d cav_pos = i.Position;
                            for (int j = 0; j < txts.Count; j++)
                            {
                                bool startsWithChinese = chineseRegex.IsMatch(txts[j].TextString);
                                bool startsWithS = letterRegex.IsMatch(txts[j].TextString);
                                if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f && !startsWithChinese && !startsWithS)
                                {
                                    widtxt1.Add(txts[j]);
                                    cav_pos = txts[j].Position;
                                    txts.RemoveAt(j);
                                    j = 0;
                                }

                            }
                        }




                        stopwatch1.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time10: {stopwatch1.ElapsedMilliseconds} ms");




                        #endregion

                        #region 表结构的端口类


                        Stopwatch stopwatch2 = new Stopwatch();
                        stopwatch2.Start(); // 开始计时



                        List<List<Port>> Ap2 = new List<List<Port>>();
                        List<Port> Ap3 = new List<Port>();//端口处理
                        foreach (List<DBText> p in xstxt1)
                        {
                            List<Port> Ap1 = new List<Port>();//端口处理
                            foreach (DBText i in p)
                            {
                                //if (i.TextString != "-")
                                {
                                    DBText item1 = new DBText();
                                    DBText item2 = new DBText();
                                    DBText item3 = new DBText();
                                    foreach (DBText j in widtxt1)
                                    {
                                        if (j.Position.X > i.Position.X && Math.Abs(j.Position.X - i.Position.X) < 25f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                        {
                                            item3 = j;
                                            break;
                                        }
                                    }
                                    foreach (DBText j in ltxt1)
                                    {
                                        if (j.Position.X > i.Position.X && Math.Abs(j.Position.X - i.Position.X) < 25f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                        {
                                            item2 = j;
                                            break;
                                        }
                                    }
                                    foreach (DBText j in porttxt1)
                                    {
                                        if (Math.Abs(j.Position.X - i.Position.X) - 6 < 2f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                        {
                                            item1 = j;
                                            break;
                                        }
                                    }
                                    Port op = new Port(i, item1, item2, item3);
                                    Ap1.Add(op);
                                    Ap3.Add(op);
                                }
                            }
                            Ap2.Add(Ap1);

                        }
                        List<List<Port>> New_Ap = new List<List<Port>>();
                        List<Port> new_ap = new List<Port>();
                        //List<bool> ap_flag = new List<bool>(Ap2.Count());
                        List<bool> ap_flag = Enumerable.Repeat(false, Ap2.Count()).ToList();

                        for (int i = 0; i < Ap2.Count() - 1; i++)
                        {
                            List<Port> ap2 = Ap2[i];
                            if (ap2.Count <= 0)
                            {
                                continue;
                            }
                            DBText a = ap2[ap2.Count() - 1].port;
                            List<Port> ap3 = Ap2[i + 1];
                            if (ap3.Count <= 0)
                            {
                                continue;
                            }
                            DBText a3 = ap3[0].port;
                            string str1 = a.TextString;
                            string str2 = a3.TextString;
                            int num1, num2;
                            bool isNum1 = int.TryParse(str1, out num1);
                            bool isNum2 = int.TryParse(str2, out num2);
                            new_ap = ap2;
                            if (isNum1 && isNum2)
                            {
                                int diff = num2 - num1;
                                if (diff == 1)
                                {
                                    if (ap_flag[i])
                                    {
                                        new_ap = New_Ap[New_Ap.Count() - 1].Concat(ap3).ToList();
                                        New_Ap.RemoveAt(New_Ap.Count() - 1);
                                    }
                                    else
                                    {
                                        new_ap = ap2.Concat(ap3).ToList();
                                    }

                                    ap_flag[i + 1] = true;
                                }

                            }
                            if (ap_flag[i] && new_ap == ap2)
                                continue;
                            New_Ap.Add(new_ap);
                        }
                        if (Ap2.Count != 0)
                        {
                            if (!ap_flag[Ap2.Count() - 1])
                                New_Ap.Add(Ap2[Ap2.Count() - 1]);
                        }

                        Ap2 = New_Ap;
                        List<List<Port>> New_Ap1 = new List<List<Port>>();//表格形式的接插件
                        List<List<Port>> Mid_Ap1 = new List<List<Port>>();//表格形式的中间件
                        foreach (List<Port> j in Ap2)
                        {
                            List<Port> new_ap1 = new List<Port>();
                            foreach (Port i in j)
                            {
                                if (i.XS.TextString != "-")
                                {
                                    new_ap1.Add(i);
                                }

                            }
                            if (j[0].port.TextString.All(char.IsDigit))
                                New_Ap1.Add(new_ap1);
                            if (j[0].port.TextString.All(char.IsLetter))
                                Mid_Ap1.Add(new_ap1);
                        }
                        Ap2 = Mid_Ap1;
                        File.Delete("n2.txt");
                        foreach (List<Port> j in Ap2)
                        {
                            foreach (Port i in j)
                            {
                                //if (i.XS.TextString != "-")
                                {
                                    File.AppendAllText("n2.txt", "线束名：" + i.XS.TextString + " " + i.XS.Position.X + " " + i.XS.Position.Y + " " + "\r\n");
                                    File.AppendAllText("n2.txt", "端口：" + i.port.TextString + " " + i.port.Position.X + " " + i.port.Position.Y + " " + "\r\n");
                                    File.AppendAllText("n2.txt", "颜色：" + i.color.TextString + " " + i.color.Position.X + " " + i.color.Position.Y + " " + "\r\n");
                                    File.AppendAllText("n2.txt", "线宽：" + i.width.TextString + " " + i.width.Position.X + " " + i.width.Position.Y + " " + "\r\n\n");
                                }

                            }
                            File.AppendAllText("n2.txt", "\n\n");
                        }
                        #endregion

                        #region 字符判断函数
                        bool isPureNum(string str)
                        {
                            bool isNumeric = int.TryParse(str, out _);
                            return isNumeric;
                        }
                        bool isfPureNum(string str)
                        {
                            bool isNumeric = float.TryParse(str, out _);
                            return isNumeric;
                        }
                        #endregion






                        //注意要改文本识别范围即dwg有效部分以及接插件名称筛选
                        #region 处理文本类，得到端口四属性以及接插件的名称(7.3加入了接插件颜色，急的修改）



                        

                        Regex regex = new Regex(@"\d+\.\d{1,2}");
                        Regex regey = new Regex("^[A-Z][a-zA-Z0-9]*$");
                        Regex regep = new Regex("^[a-zA-Z][a-zA-Z0-9][a-zA-Z]*$");
                        Regex regez = new Regex("^[N][1-9][0-9]*$");
                        //Regex regeq = new Regex(@"^[\u4e00-\u9fa5]+(-[A-Z0-9]+)?$");//?表示可选第一种带-或第二种不带-
                        Regex regeq = new Regex(@"^[\u4e00-\u9fa5]+([-\s][A-Z0-9]+)?$");
                        Regex regew = new Regex(@"^[a-zA-Z]+[\u4e00-\u9fa5]+$");
                        Regex regee = new Regex(@"^[a-zA-Z][0-9]+[\u4e00-\u9fa5]+$");
                        Regex reger = new Regex(@"^[\u4e00-\u9fa5]+[A-Z]+[\+]+$");
                        Regex reget = new Regex(@"^[\u4e00-\u9fa5]+[\（]+[\u4e00-\u9fa5]+[\）]+$");
                        Regex regeu = new Regex("^[0-9]+[a-zA-Z]+[0-9]+$");
                        Regex regei = new Regex("^[a-zA-Z]+[0-9]+[a-zA-Z]?$");
                        Regex regen = new Regex(@"^[\u4e00-\u9fa5]+[A-Z0-9]+$");
                        Regex regeo = new Regex(@"^\d+[A-Z]+[\u4e00-\u9fa5]+$");
                        Regex singleChineseCharRegex = new Regex("^[\u4e00-\u9fa5]$");
                        Regex regea = new Regex("^[\\u4e00-\\u9fa5]+[A-Z]+[\\u4e00-\\u9fa5]+(?:\\（[A-Z]\\）)?$");
                        Regex regeb = new Regex("^[\u4e00-\u9fa5]+[0-9]+[\u4e00-\u9fa5]+$");
                        Regex regec = new Regex("^[0-9]+[\u4e00-\u9fa5]+$");
                        Regex reged = new Regex(@"^SP-.*$");
                        Regex rege_mix3 = new Regex("^[A-Z][a-zA-Z0-9\\\\]*$");
                        Regex rege_mix2 = new Regex(@"^\d+\.\d+\s*[A-Z]{1,2}(/?[A-Z])?$");
                        List<DBText> widtxt = new List<DBText>();
                        List<DBText> porttxt = new List<DBText>();
                        List<DBText> ltxt = new List<DBText>();
                        List<DBText> xstxt = new List<DBText>();
                        List<DBText> vetextxt = new List<DBText>();
                        List<DBText> biaogevetextxt = new List<DBText>();
                        //double leftx = 270.151;//请主动输入
                        //double rightx = 2879.2266;
                        //double portmax = 160;//最大端口号
                        //double txt_heighe = 2.7344;

                        //double leftx = 226.3;//请主动输入
                        //double rightx = 2631;
                        //double portmax = 160;//最大端口号
                        //double txt_heighe = 2.7344;

                        //double leftx = 235;//请主动输入
                        //double rightx = 5200;
                        //double portmax = 160;//最大端口号
                        //double txt_heighe = 3.1477;

                        //double leftx = 5038;//请主动输入
                        //double rightx = 5886;
                        //double portmax = 160;//最大端口号
                        //double txt_heighe = 3.5;//接插件文本高度,颜色也要
                        //double downx = -5377;
                        //线束颜色最多四位
                        //线束标号最多六位
                        //重复名称，确定文本高度
                        //三处去除相似文本

                        File.Delete("TextString.txt");
                        File.Delete("vetextxt.txt");
                        foreach (DBText i in txts)
                        {
                            File.AppendAllText("TextString.txt", i.TextString + "\r\n");

                            if ((rege_mix2.IsMatch(i.TextString) && i.TextString.Length >= 5) ||
                                ((rege_mix3.IsMatch(i.TextString) && !regez.IsMatch(i.TextString)) && i.TextString.Length <= 20 && i.TextString.Length > 6))
                            {
                                DBText name = new DBText();
                                DBText color = new DBText();
                                DBText width = new DBText();
                                //不能直接复制，因为会导致引用同一个变量
                                // 复制i的所有属性到新的DBText对象中
                                name.AlignmentPoint = i.AlignmentPoint;
                                name.HorizontalMode = i.HorizontalMode;
                                name.Position = i.Position;
                                name.Rotation = i.Rotation;
                                name.TextStyleId = i.TextStyleId;
                                name.Thickness = i.Thickness;
                                name.VerticalMode = i.VerticalMode;
                                name.WidthFactor = i.WidthFactor;
                                name.Height = i.Height;

                                color.AlignmentPoint = i.AlignmentPoint;
                                color.HorizontalMode = i.HorizontalMode;
                                color.Position = i.Position;
                                color.Rotation = i.Rotation;
                                color.TextStyleId = i.TextStyleId;
                                color.Thickness = i.Thickness;
                                color.VerticalMode = i.VerticalMode;
                                color.WidthFactor = i.WidthFactor;
                                color.Height = i.Height;

                                width.AlignmentPoint = i.AlignmentPoint;
                                width.HorizontalMode = i.HorizontalMode;
                                width.Position = i.Position;
                                width.Rotation = i.Rotation;
                                width.TextStyleId = i.TextStyleId;
                                width.Thickness = i.Thickness;
                                width.VerticalMode = i.VerticalMode;
                                width.WidthFactor = i.WidthFactor;
                                width.Height = i.Height;
                                //判断文本是不是合并起来的文本
                                //匹配以大写字母开头，后面可以是任意组合的字母、数字或反斜杠的字符串。
                                //字符串长度要小于20，大于6的
                                //只要包含斜杠的了，就说明已经将线色也合并在一起了
                                //以下条件有点宽泛
                                if ((rege_mix3.IsMatch(i.TextString) && !regez.IsMatch(i.TextString)) && i.TextString.Length <= 20 && i.TextString.Length > 6 && i.TextString.Contains('.'))
                                {
                                    // 使用空格和\\P分割字符串
                                    string[] parts = i.TextString.Split(new[] { "\\P", " " }, StringSplitOptions.None);
                                    name.TextString = parts.Length > 0 ? parts[0] : string.Empty; // 第一部分
                                    width.TextString = parts.Length > 1 ? parts[1] : string.Empty; // 第二部分
                                    color.TextString = parts.Length > 2 ? parts[2] : string.Empty; // 第二部分
                                                                                                   //拆分字符串
                                    widtxt.Add(width);
                                    ltxt.Add(color);
                                    xstxt.Add(name);
                                }
                                //线宽与颜色合并了的
                                else if (rege_mix2.IsMatch(i.TextString) && i.TextString.Length >= 5)
                                {
                                    // 使用正则表达式提取线宽和颜色
                                    //string pattern = @"^(\d+\.\d+)\s*([A-Z]{1,2}(/?[A-Z]))?$";
                                    string pattern = @"(\d+\.\d+)\s*([A-Z]{1,2}(/[A-Z])?)";
                                    Regex regex2 = new Regex(pattern);
                                    Match match = regex2.Match(i.TextString);
                                    if (match.Success)
                                    {
                                        // 提取线宽和颜色
                                        string data_width = match.Groups[1].Value;
                                        string data_color = match.Groups[2].Value;
                                        width.TextString = data_width;
                                        color.TextString = data_color;
                                        widtxt.Add(width);
                                        ltxt.Add(color);
                                    }

                                }

                            }
                            //正常情况
                            else
                            {
                                if (i.Position.X > leftx && i.Position.X < rightx /*&& i.Position.Y > downx*/ && i.TextString.Length > 0)
                                {
                                    if (regex.Matches(i.TextString) != null && regex.Matches(i.TextString).Count > 0 && isfPureNum(i.TextString))
                                        widtxt.Add(i);
                                    int num = int.TryParse(i.TextString, out num) ? num : 0;
                                    if (isPureNum(i.TextString) && num < portmax && num > 0)
                                        porttxt.Add(i);
                                    if (i.TextString.Length == 1 && char.IsUpper(i.TextString[0]))
                                    {
                                        porttxt.Add(i);
                                    }
                                    if (i.TextString.Contains('/'))
                                    {
                                        //如果文本中包含/字符，使用正则表达式检查是否全部由字母和/组成，并且长度小于5，满足条件则添加到ltxt。j
                                        if (Regex.IsMatch(i.TextString, @"^[a-zA-Z/]+$") && i.TextString.Length < 5)
                                            ltxt.Add(i);
                                    }
                                    else
                                    {
                                        //如果不包含/，则检查是否全部由字母组成且长度不超过2，满足则添加到ltxt。j
                                        if (Regex.IsMatch(i.TextString, @"^[a-zA-Z]+$") && i.TextString.Length <= 2)
                                            ltxt.Add(i);
                                    }
                                    //如果字符串以大写字母开头，后接任意组合的字母或数字，并且长度不超过6个字符，
                                    //但不匹配regez指定的格式（即不是以N开头后接1到3的数字和其他任意数字的字符串），则满足添加条件。

                                    //如果字符串符合以数字开始，中间有字母，最后以数字结束的模式，也满足添加条件。
                                    if ((regey.IsMatch(i.TextString) && i.TextString.Length <= 6 && !regez.IsMatch(i.TextString)) || regeu.IsMatch(i.TextString))
                                    {
                                        bool hasLetter = false;
                                        bool hasDigit = false;
                                        //此外，这个文本还必须包含至少一个字母和一个数字，并且不是"E115"。
                                        foreach (char c in i.TextString)
                                        {
                                            if (char.IsLetter(c))
                                            {
                                                hasLetter = true;
                                            }
                                            else if (char.IsDigit(c))
                                            {
                                                hasDigit = true;
                                            }
                                            if (i.TextString == "E115")
                                                break;
                                            if (hasLetter && hasDigit)
                                            {
                                                //这里的xstxt是不是有问题导致后面坐标之间的运算有问题,也可能是ltxt的问题,这里是i
                                                xstxt.Add(i);
                                                break;
                                            }
                                        }
                                    }
                                    //if (Math.Abs(i.Height - txt_heighe) < 0.1f && i.Color.Red == 0 && i.Color.Green == 255 && i.Color.Blue == 0)//只为BD
                                    //{
                                    //    vetextxt.Add(i);
                                    //}
                                    //if (Math.Abs(i.Height - txt_heighe) < 0.1f && i.Color.Red == 255 && i.Color.Green == 0 && i.Color.Blue == 0)//只为BD
                                    //{
                                    //    vetextxt.Add(i);
                                    //}
                                    //regei和第一张图冲突
                                    /*
                                     * 匹配仅由中文字符组成的字符串，或者中文字符后跟一个由破折号和大写英文字母或数字组成的后缀。
                                     * 匹配以英文字母开头，后跟一串中文字符的字符串。
                                     * 匹配以单个英文字母开头，后接数字，并以中文字符结束的字符串。
                                     * 匹配由中文字符开头，后跟大写英文字母，最后以一个或多个加号+结束的字符串。
                                     * 匹配由中文字符组成的串，中间包含括号内的中文字符。
                                     * 文本高度与特定值txt_heighe的差距小于0.01f。
                                     */

                                    if (Math.Abs(i.Height - txt_heighe) < 0.01f && reged.IsMatch(i.TextString))
                                    {
                                        biaogevetextxt.Add(i);

                                        File.AppendAllText("vetextxt.txt", "接插件名：" + i.TextString + "\r\n");

                                    }
                                    if (/*i.Color.Red==255&&*/Math.Abs(i.Height - txt_heighe) < 0.01f && (regec.IsMatch(i.TextString) || regeb.IsMatch(i.TextString) || regea.IsMatch(i.TextString) || regeo.IsMatch(i.TextString) || regen.IsMatch(i.TextString) || regeq.IsMatch(i.TextString) || regew.IsMatch(i.TextString) || regee.IsMatch(i.TextString) || reger.IsMatch(i.TextString) || i.TextString == "水温传感器 !PTC" || reget.IsMatch(i.TextString)) && !singleChineseCharRegex.IsMatch(i.TextString) && i.TextString != "分支视图B" && i.TextString != "分支视图A" && i.TextString != "分支视图C" && i.TextString != "以上焊点做在保险丝盒内" && i.TextString != "大端子双壁热缩" && i.TextString != "用PVC胶带密绕捆扎" && i.TextString != "端子需双壁热缩" && i.TextString != "端子需双壁热缩" && i.TextString != "单壁热缩" && i.TextString != "到线束主干上" && i.TextString != "此处分支走向参考视图A" && i.TextString != "视图B" && i.TextString != "视图A" && i.TextString != "双壁热缩" && i.TextString != "以上焊点做在保险丝盒内" && i.TextString != "汉高4型泰乐松丁基胶泥" && i.TextString != "汉高4型泰勒松丁基泥胶" && i.TextString != "终端电阻" && i.TextString != "测量基准点" && i.TextString != "上盖" && i.TextString != "装配" && i.TextString != "防水捆扎3层" && i.TextString != "橡胶件" && i.TextString != "扎带" && i.TextString != "护套" && i.TextString != "端子" && i.TextString != "防水栓" && i.TextString != "盲栓" && i.TextString != "扣花" && i.TextString != "卡扣" && i.TextString != "磁环" && i.TextString != "自锁" && i.TextString != "卡花" && i.TextString != "大端子" && i.TextString != "小端子" && i.TextString != "定位件安装图" && i.TextString != "定位件列表" && i.TextString != "数量" && i.TextString != "零件号" && i.TextString != "绒布-19" && i.TextString != "USB接口" && i.TextString != "驾驶员状态监测屏蔽线" && i.TextString != "USB屏蔽线" && i.TextString != "分支方向垂直直面向下" && i.TextString != "倒车摄像头屏蔽线-III" && i.TextString != "4G天线" && i.TextString != "收音天线横梁馈线总成" && i.TextString != "绿色胶带标记" && i.TextString != "分支方向垂直纸面向里" && i.TextString != "E115")
                                    {
                                        vetextxt.Add(i);
                                        File.AppendAllText("vetextxt.txt", "接插件名：" + i.TextString + "\r\n");
                                    }
                                    if (Math.Abs(i.Height - txt_heighe) < 0.01f && regei.IsMatch(i.TextString) && !regez.IsMatch(i.TextString) && i.TextString.Length <= 6 && i.TextString != "BX2402" && i.TextString != "装配" && i.TextString != "橡胶件" && i.TextString != "定位件安装图" && i.TextString != "定位件列表" && i.TextString != "数量" && i.TextString != "零件号" && i.TextString != "绒布-19" && i.TextString != "USB屏蔽线" && i.TextString != "USB接口")
                                    {
                                        vetextxt.Add(i);
                                        File.AppendAllText("vetextxt.txt", "接插件名：" + i.TextString + "\r\n");
                                    }
                                }

                            }
                        }

                        stopwatch2.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time20: {stopwatch2.ElapsedMilliseconds} ms");

                        #endregion

                        //此处一个端口里各属性的距离需特化,第一张图为固定，line图为范围
                        #region 构建端口类并输出




                        Stopwatch stopwatch3 = new Stopwatch();
                        stopwatch3.Start(); // 开始计时





                        //也有可能是基准没选对，也就是i
                        AllPort Ap = new AllPort();//端口处理
                        foreach (DBText i in xstxt)
                        {
                            //这里是没有包含P1的第一个原因，因为vetextxt也包含了。
                            /*                            if (vetextxt.Contains(i))
                                                            continue;*/
                            DBText item1 = new DBText();
                            DBText item2 = new DBText();
                            DBText item3 = new DBText();
                            double minDist = double.MaxValue;
                            foreach (DBText j in widtxt)
                            {
                                //double dist = Math.Sqrt(Math.Pow(j.Position.X - i.Position.X, 2) + Math.Pow(j.Position.Y - i.Position.Y, 2));
                                //if (dist < minDist)
                                //{
                                //    minDist = dist;
                                //    item3 = j;
                                //}
                                //if (Math.Abs(j.Position.X - i.Position.X) - 5.5 < 1f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                                //{
                                //    item3 = j;
                                //    break;
                                //}
                                //if (Math.Abs(j.Position.X - i.Position.X) < 6f && Math.Abs(j.Position.Y - i.Position.Y) < 6f)
                                if ((Math.Pow((j.Position.X - i.Position.X), 2) + Math.Pow((j.Position.Y - i.Position.Y), 2)) < 72f && j.Position.Y > i.Position.Y && j.Position.X > i.Position.X)
                                {
                                    item3 = j;
                                    break;
                                }
                            }

                            foreach (DBText j in ltxt)
                            {
                                //if (Math.Abs(j.Position.X - i.Position.X) - 1.8 < 1f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                                //{
                                //    item2 = j;
                                //    break;
                                //}
                                //是不是因为这里一直都没满足条件，所以item2一直是0，导致color的3个坐标一直是0
                                //if (Math.Abs(j.Position.X - i.Position.X) < 10f && Math.Abs(j.Position.Y - i.Position.Y) < 10f)
                                if ((Math.Pow((j.Position.X - i.Position.X), 2) + Math.Pow((j.Position.Y - i.Position.Y), 2)) < 200f && j.Position.Y > i.Position.Y)
                                {
                                    item2 = j;
                                    break;
                                }
                            }

                            foreach (DBText j in porttxt)
                            {
                                //if (Math.Abs(j.Position.X - i.Position.X) - 2.5 < 2f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                                //{SS
                                //    item1 = j;
                                //    break;
                                //}
                                //if (Math.Abs(j.Position.X - i.Position.X) - 2.5 < 5f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                                if ((Math.Pow((j.Position.X - i.Position.X), 2) + Math.Pow((j.Position.Y - i.Position.Y), 2)) < 65f && j.Position.Y > i.Position.Y && j.Position.X < i.Position.X)
                                {
                                    item1 = j;
                                    break;
                                }
                            }
                            Port op = new Port(i, item1, item2, item3);//这里的四个元素对应于XS，port，color，widt
                            Ap.Add(op);
                        }


                        File.Delete("ttc.txt");
                        foreach (Port i in Ap.add)
                        {
                            File.AppendAllText("ttc.txt", "线束名：" + i.XS.TextString + " " + i.XS.Position.X + " " + i.XS.Position.Y + " " + "\r\n");
                            File.AppendAllText("ttc.txt", "端口：" + i.port.TextString + " " + i.port.Position.X + " " + i.port.Position.Y + " " + "\r\n");
                            File.AppendAllText("ttc.txt", "颜色：" + i.color.TextString + " " + i.color.Position.X + " " + i.color.Position.Y + " " + "\r\n");
                            File.AppendAllText("ttc.txt", "线宽：" + i.width.TextString + " " + i.width.Position.X + " " + i.width.Position.Y + " " + "\r\n\n");
                        }

                        stopwatch3.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time30: {stopwatch3.ElapsedMilliseconds} ms");

                        #endregion

                        #region 线束分组函数（字符位置在包围盒内分为一组）

                        Stopwatch stopwatch4 = new Stopwatch();
                        stopwatch4.Start(); // 开始计时

                        // 通过射线法判断一个点是否在多边形内
                        bool IsPointInsidePolygon(Point3d point, Polyline polygon)
                        {
                            int n = polygon.NumberOfVertices;
                            bool inside = false;
                            double xinters = 0; // 初始化 xinters 变量
                            Point3d p1, p2;

                            p1 = polygon.GetPoint3dAt(0);
                            for (int i = 1; i <= n; i++)
                            {
                                p2 = polygon.GetPoint3dAt(i % n); // 获取下一个点（循环回到第一个点）

                                // 检查点是否在 y 轴的区间内
                                if (point.Y > Math.Min(p1.Y, p2.Y))
                                {
                                    if (point.Y <= Math.Max(p1.Y, p2.Y))
                                    {
                                        if (point.X <= Math.Max(p1.X, p2.X))
                                        {
                                            if (p1.Y != p2.Y) // 如果 p1.Y 不等于 p2.Y 才计算交点
                                            {
                                                // 计算交点的 X 坐标
                                                xinters = (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y) + p1.X;
                                            }
                                            // 如果 X 坐标小于交点，或者两个点在同一竖直线上
                                            if (p1.X == p2.X || point.X <= xinters)
                                            {
                                                inside = !inside; // 如果射线与多边形的边相交，反转 inside 状态
                                            }
                                        }
                                    }
                                }
                                p1 = p2; // 更新 p1 为当前的 p2
                            }
                            return inside; // 如果 inside 为 true，表示点在多边形内
                        }
                        // 新的分组方法，不依赖于 GroupPoints，而是使用 mergedPolylines 来分组
                        List<List<Port>> GroupPortsByPolygons(List<Port> xs)
                        {
                            List<List<Port>> groups = new List<List<Port>>();  // 用于存储分组结果

                            // 遍历每个多边形（包围盒）
                            //foreach (Polyline polygon in mergedPolylines)
                            //{
                            //    List<Port> group = new List<Port>();  // 用于存储与当前多边形匹配的 Port 对象

                            //    // 遍历 ooo 中的每个 Port
                            //    foreach (Port port in xs)
                            //    {
                            //        // 判断该 Port 是否在当前多边形内
                            //        if (IsPointInsidePolygon(port.XS.Position, polygon))
                            //        {
                            //            group.Add(port);  // 如果在多边形内，加入当前组
                            //        }
                            //    }

                            //    if (group.Count > 0)
                            //    {
                            //        groups.Add(group);  // 如果当前组不为空，加入到分组列表中
                            //    }
                            //}





                            foreach (PolylineWithIndex polylineWithIndex in mergedPolylinesWithIndex)
                            {
                                List<Port> group = new List<Port>();  // 用于存储与当前多边形匹配的 Port 对象

                                // 遍历 ooo 中的每个 Port
                                foreach (Port port in xs)
                                {
                                    // 判断该 Port 是否在当前多边形内
                                    if (IsPointInsidePolygon(port.XS.Position, polylineWithIndex.Polyline))
                                    {
                                        group.Add(port);  // 如果在多边形内，加入当前组
                                    }
                                }

                                if (group.Count > 0)
                                {
                                    groups.Add(group);  // 如果当前组不为空，加入到分组列表中
                                }
                            }













                            return groups;
                        }

                        stopwatch4.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time40: {stopwatch4.ElapsedMilliseconds} ms");
                        #endregion


                        #region 是否靠近和分组函数，参数为点列表和判断距离
                        //下面的代码是根据距离将端口归为一组
                        bool IsClose(Point3d p1, Point3d p2, double distanceThreshold)
                        {
                            //计算两个点的距离
                            double distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
                            //距离是否小于阈值
                            return distance <= distanceThreshold;
                        }//判断两点之间的距离是否小于或等于阈值
                        List<List<Port>> GroupPoints(List<Port> points, double distanceThreshold, Point3d box_point_temp)
                        {
                            //定义一个group列表，用于存储分类结果
                            List<List<Port>> groups = new List<List<Port>>();
                            //定义一个集合保存已经处理过的点
                            HashSet<Point3d> visited = new HashSet<Point3d>();
                            //在这里添加一个逻辑，先处理在主保险丝盒总成里的线束点（需要添加一个主保险丝盒的顶点坐标参数）
                            if (box_point_temp.X != 0 && box_point_temp.Y != 0)
                            {
                                List<Port> box_group = new List<Port>();
                                foreach (Port point in points)
                                {
                                    if (IsClose(box_point_temp, point.XS.Position, 380))
                                    {
                                        box_group.Add(point);
                                        visited.Add(point.XS.Position);
                                    }
                                }
                                if (box_group.Count > 0)
                                {
                                    groups.Add(box_group);
                                }
                            }
                            //遍历每一个点
                            foreach (Port point in points)
                            {
                                //如果这个点没有被处理过
                                if (!visited.Contains(point.XS.Position))
                                {
                                    //创建一个新的组，并将当前点添加到组中
                                    List<Port> group = new List<Port>();
                                    group.Add(point);
                                    //将当前点标志为已经访问
                                    visited.Add(point.XS.Position);
                                    //使用一个内部循环，从当前组的第一个点开始，遍历组中每一个点
                                    for (int i = 0; i < group.Count; i++)
                                    {
                                        //继续遍历其余所有节点
                                        Point3d current = group[i].XS.Position;
                                        //如果其余节点没有被处理过，就计算这个点与当前点之间的距离是否小于等于阈值，小于就添加到同一组里
                                        foreach (Port other in points)
                                        {
                                            if (!visited.Contains(other.XS.Position) && IsClose(current, other.XS.Position, distanceThreshold))
                                            {
                                                group.Add(other);
                                                visited.Add(other.XS.Position);
                                            }
                                        }
                                    }

                                    groups.Add(group);
                                }
                            }

                            return groups;
                        }//计算空间中各点之间的距离，将 Port 对象根据给定的距离阈值进行分组
                        #endregion

                        #region 筛选中间端口和接插件端口
                        List<Port> ooo = new List<Port>();
                        //ooom是中间端口
                        List<DBText> ooom = new List<DBText>();
                        File.Delete("XS_TextString.txt");
                        File.Delete("elements.txt");
                        foreach (Port i in Ap.add)
                        {
                            if (i.color.Position.X != 0)//为何是线色？
                            {
                                ooo.Add(i);
                                File.AppendAllText("XS_TextString.txt", "线束名：" + i.XS.TextString + "\r\n");
                                File.AppendAllText("elements.txt", +i.XS.Position.X + "," + i.XS.Position.Y + "\r\n");
                            }
                            else
                                ooom.Add(i.XS);
                        }
                        #endregion
                        //ooo在这里就已经是0了，没有数据
                        #region 根据中间端口构建中间件列表
                        bool poppb = false;
                        DBText popp = new DBText();
                        List<DBText> iio = new List<DBText>();
                        List<List<DBText>> iiom = new List<List<DBText>>();
                        foreach (var i in ooom)
                        {

                            if (!poppb)
                            {
                                iio = new List<DBText>();
                                iio.Add(i);
                                popp = i;
                                poppb = true;
                            }
                            else
                            {
                                //if (Math.Abs(popp.Position.X - i.Position.X) < 1 && Math.Abs(Math.Abs(popp.Position.Y - i.Position.Y) - 4) < 1)
                                if ((Math.Pow((popp.Position.X - i.Position.X), 2) + Math.Pow((i.Position.Y - i.Position.Y), 2)) < 26f)
                                {
                                    iio.Add(i);
                                    popp = i;

                                }
                                else
                                {
                                    iiom.Add(iio);
                                    iio = new List<DBText>();
                                    iio.Add(i);
                                    popp = i;
                                    poppb = true;
                                }

                            }


                        }
                        #endregion

                        //需指定分组时各端口距离
                        #region 端口按相对距离分组
                        
                        //查找是否有主保险丝盒
                        Point3d box_point = new Point3d(0, 0, 0);
                        /*                        foreach (DBText pttxt in vetextxt)
                                                {
                                                    if(pttxt.TextString == "主保险丝盒总成" && pttxt.Position.X > 2000)
                                                    {

                                                        box_point = new Point3d(pttxt.Position.X, pttxt.Position.Y, 0);
                                                        break;
                                                    }
                                                }*/
                        //分组函数调用
                        List<List<Port>> ppp = GroupPortsByPolygons(ooo);
                        File.Delete("tt.txt");
                        File.Delete("n1.txt");
                        File.AppendAllText("n1.txt", ppp.Count + "\n");
                        foreach (List<Port> j in ppp)
                        {
                            File.AppendAllText("n1.txt", j.Count + "\n");
                            for (int i = 0; i < j.Count; i++)
                            {
                                File.AppendAllText("n1.txt", j[i].XS.TextString + " " + "\n");
                            }
                            /*                            if (j.Count > 1)
                                                            File.AppendAllText("n1.txt", j[1].XS.TextString + " " + "\n");
                                                        File.AppendAllText("n1.txt", "\n");*/
                        }

                        #endregion

                        #region 判断点在线的上方 
                        bool IsPointAboveLineSegment(Point3d p1, Point3d p2, Point3d p)
                        {
                            if (p1.X == p2.X) // 直线竖直
                            {
                                return p.X < p1.X;
                            }
                            else // 直线非竖直
                            {
                                // 计算直线段的斜率和截距
                                double a = (p2.Y - p1.Y) / (p2.X - p1.X);
                                double b = p1.Y - a * p1.X;

                                // 判断点是否在直线的上方
                                bool isAboveLine = p.Y > a * p.X + b;

                                // 判断点是否在直线段的两个端点之间
                                bool isBetweenPoints = (p.X >= p1.X && p.X <= p2.X) || (p.X >= p2.X && p.X <= p1.X);

                                return isAboveLine && isBetweenPoints;
                            }
                        }

                        bool GetPointIsInLine(Point3d pf, Point3d p1, Point3d p2, double range)
                        {
                            //range 判断的的误差，不需要误差则赋值0
                            //点在线段首尾两端之外则return false
                            double cross = (p2.X - p1.X) * (pf.X - p1.X) + (p2.Y - p1.Y) * (pf.Y - p1.Y);
                            if (cross <= 0) return false;
                            double d2 = (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
                            if (cross >= d2) return false;
                            double r = cross / d2;
                            double px = p1.X + (p2.X - p1.X) * r;
                            double py = p1.Y + (p2.Y - p1.Y) * r;
                            //判断距离是否小于误差
                            return (pf.X - px) * (pf.X - px) + (py - pf.Y) * (py - pf.Y) <= range * range;
                        }
                        #endregion


                        #region 7.3筛选hatch拉伸点
                        //从 Hatchs 列表中去除重复或距离过近的点，将符合条件的点添加到 new_hatch 列表中
                        List<Point3d> new_hatch = new List<Point3d>();
                        foreach (var i in Hatchs)
                        {
                            if (new_hatch.Count == 0)
                            {
                                new_hatch.Add(i);
                            }
                            else
                            {
                                bool ffl = false;
                                foreach (Point3d j in new_hatch)
                                {
                                    if (IsClose(i, j, 1))
                                    {
                                        ffl = true;
                                    }
                                }
                                if (!ffl)
                                    new_hatch.Add(i);
                            }
                        }


                        //foreach(var i in new_hatch)
                        //    File.AppendAllText("ttp.txt", i.X + " " + i.Y + "\r\n"/*, Encoding.Default*/);
                        List<my_line> new_line = new List<my_line>();
                        foreach (var i in my_lines)
                        {
                            bool fppf = false;
                            List<Point3d> new_midq = new List<Point3d>();
                            if (i.line.Length > 15)
                            {
                                foreach (var j in new_hatch)
                                {
                                    if (DistancePointToLineSegment(j, i.line.StartPoint, i.line.EndPoint) < 0.4f)
                                    {

                                        Point3d midq = CalculateProjection(i.line.StartPoint, i.line.EndPoint, j);
                                        new_midq.Add(midq);
                                        //Line midline = new Line(i.line.StartPoint, midq);
                                        //my_line p1;
                                        //p1.line = midline;
                                        //p1.visit = false;
                                        //new_line.Add(p1);
                                        //midline = new Line(midq, i.line.EndPoint);
                                        //p1.line = midline;
                                        //new_line.Add(p1);
                                        fppf = true;

                                    }

                                }
                            }
                            if (!fppf)
                                new_line.Add(i);
                            else
                            {
                                new_midq.Add(i.line.StartPoint);
                                new_midq.Add(i.line.EndPoint);
                                new_midq.Sort((p1, p2) => p1.X.CompareTo(p2.X));
                                File.AppendAllText("ttp.txt", new_midq.Count() + "\r\n"/*, Encoding.Default*/);
                                for (int j = 0; j < new_midq.Count() - 1; j++)
                                {
                                    Line midline = new Line(new_midq[j], new_midq[j + 1]);
                                    my_line p1;
                                    p1.line = midline;
                                    p1.visit = false;
                                    new_line.Add(p1);
                                }
                            }



                        }

                        #endregion


                        //(修改！！)路径长度的txt高度需特化,无向图需要给定一个起始点
                        #region 构建无向图（line/polyline)选一下




                        Stopwatch stopwatch5 = new Stopwatch();
                        stopwatch5.Start(); // 开始计时




                        List<DBText> numtxt = new List<DBText>();
                        //黄色去掉
                        foreach (DBText i in txts)
                        {
                            if (isPureNum(i.TextString) && Math.Abs(i.Height - quanzhi_height) < 0.1f/*&&((i.Color.Red==255&& i.Color.Green == 0 && i.Color.Blue == 0)|| (i.Color.Red == 255 && i.Color.Green == 255 && i.Color.Blue == 255))*/)
                                numtxt.Add(i);
                        }
                        List<tuple> tuple = new List<tuple>();//用于表示一对点，可能是用于路径的起点和终点。
                        List<string> wei = new List<string>();//存储每个路径点所关联的数字字符串或者 "0"
                        Queue<Point3d> findpoint = new Queue<Point3d>();//队列来执行广度优先搜索或路径查找，初始化时加入起始点 startpoint
                        findpoint.Enqueue(startpoint);

                        if (lop)
                        {
                            Entity selectedEntity = tr.GetObject(GlobalEntityData.SelectedEntityId, OpenMode.ForRead) as Entity;
                            if (selectedEntity == null || !(selectedEntity is Line selectedLine))
                            {
                                MessageBox.Show("The selected entity is not a Line.", "Error");
                                return;
                            }

                            // 获取所有线段
                            List<Line> allLines = my_lines.Select(ml => ml.line).ToList();

                            /*// 计算四叉树的边界
                            double xMin = double.MaxValue, yMin = double.MaxValue, xMax = double.MinValue, yMax = double.MinValue;
                            foreach (Line line in allLines)
                            {
                                Extents3d bounds = line.GeometricExtents;
                                xMin = Math.Min(xMin, bounds.MinPoint.X);
                                yMin = Math.Min(yMin, bounds.MinPoint.Y);
                                xMax = Math.Max(xMax, bounds.MaxPoint.X);
                                yMax = Math.Max(yMax, bounds.MaxPoint.Y);
                            }

                            // 初始化四叉树
                            QuadTreeNode<Line> quadTree = new QuadTreeNode<Line>(xMin, yMin, xMax - xMin, yMax - yMin, 10, 5);

                            // 将所有线段插入四叉树
                            foreach (Line line in allLines)
                            {
                                quadTree.AddNode(line.GeometricExtents.MinPoint, line);
                            }*/

                            // 找到所有连接线段
                            HashSet<Line> connectedLines = FindConnectedLines(selectedLine, allLines);


                            // 更新 new_line 中的 visit 属性
                            for (int k = 0; k < my_lines.Count; k++)
                            {
                                if (connectedLines.Contains(my_lines[k].line))
                                {
                                    my_line updated = my_lines[k];
                                    updated.visit = true;
                                    my_lines[k] = updated;
                                }
                            }

                            // 初始化点为基础的BFS

                            tuple.Clear();
                            wei.Clear();


                            // 遍历每个连接线段，匹配权值文本并记录到 tuple 和 wei
                            foreach (Line line in connectedLines)
                            {
                                // 创建 tuple，记录线段的起点和终点
                                tuple midtuple = new tuple { start = line.StartPoint, end = line.EndPoint };
                                tuple.Add(midtuple);

                                // 查找附近的权值文本
                                bool pp = false;
                                foreach (DBText p in numtxt)
                                {
                                    if (DistancePointToLineSegment(p.Position, line.StartPoint, line.EndPoint) < wei_dis)
                                    {
                                        wei.Add(p.TextString);
                                        pp = true;
                                        break; // 添加第一个匹配的权值文本
                                    }
                                }
                                if (!pp)
                                    wei.Add("0"); // 如果没有匹配的权值文本，添加 "0"
                            }
                        }
                        else 
                        {
                           while (findpoint.Count != 0)
                           {
                                    //从队列中取出当前点作为 midpoint，并将其作为 midtuple 的起点。
                                    Point3d midpoint = findpoint.Dequeue();
                                    tuple midtuple;
                                    midtuple.start = midpoint;
                                    int j = 0;//用于限制每次查找的最大路径数，防止无限循环。

                                    /*if (lop)
                                    {
                                        //line寻径
                                        for (int ij = 0; ij < new_line.Count; ij++)//遍历new_line
                                        {
                                            my_line i = new_line[ij];
                                            // 如果当前线段未被访问过，且线段起点与当前中点接近，则继续查找
                                            if (!i.visit)
                                            {
                                                // 判断中点是否与线段的起点接近（X和Y坐标差值小于0.04f）
                                                if (Math.Abs(midpoint.X - i.line.StartPoint.X) < 0.04f && Math.Abs(midpoint.Y - i.line.StartPoint.Y) < 0.04f)
                                                {
                                                    //如果接近，则将线段的终点加入到查找队列
                                                    findpoint.Enqueue(i.line.EndPoint);
                                                    // 设置当前路径的终点为线段的终点
                                                    midtuple.end = i.line.EndPoint;
                                                    bool pp = false;// 用于判断当前路径段是否与数字文本有关
                                                    foreach (DBText p in numtxt)
                                                    {
                                                        // 判断当前数字文本p的位置与路径线段的距离是否小于指定阈值wei_dis
                                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis *//*&&IsPointAboveLineSegment(midtuple.start, midtuple.end, p.Position)*//*)
                                                        {
                                                            wei.Add(p.TextString);
                                                            pp = true;
                                                            break;
                                                        }
                                                    }
                                                    // 如果没有找到相关文本，则添加“0”表示没有相关文本
                                                    if (!pp)
                                                        wei.Add("0");
                                                    // 将当前路径的起点和终点添加到tuple列表
                                                    tuple.Add(midtuple);

                                                    // 标记当前线段为已访问
                                                    i.visit = true;

                                                    // 更新line列表中该线段的状态
                                                    new_line[ij] = i;

                                                    // 增加路径查找的计数器
                                                    j++;

                                                    // 如果查找的路径次数达到6次，则停止进一步查找
                                                    if (j >= 6)
                                                    {
                                                        break;
                                                    }
                                                }
                                                // 如果当前中点接近线段的终点，则反向查找
                                                else if (Math.Abs(midpoint.X - i.line.EndPoint.X) < 0.04f && Math.Abs(midpoint.Y - i.line.EndPoint.Y) < 0.04f)
                                                {
                                                    // 将线段的起点加入到查找队列
                                                    findpoint.Enqueue(i.line.StartPoint);

                                                    // 设置当前路径的终点为线段的起点
                                                    midtuple.end = i.line.StartPoint;

                                                    // 用于判断当前路径段是否与数字文本有关
                                                    bool pp = false;
                                                    foreach (DBText p in numtxt)
                                                    {
                                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis *//* && IsPointAboveLineSegment(midtuple.start, midtuple.end, p.Position)*//*)
                                                        {
                                                            wei.Add(p.TextString);
                                                            pp = true;
                                                            break;
                                                        }
                                                    }
                                                    if (!pp)
                                                        wei.Add("0");
                                                    tuple.Add(midtuple);
                                                    i.visit = true;
                                                    new_line[ij] = i;
                                                    continue;

                                                }
                                            }
                                        }
                                    }*/

                                    //else
                                    {
                                        for (int ij = 0; ij < Polys.Count; ij++)
                                        {
                                            Poly i = Polys[ij];// 获取当前检查的多段线

                                            // 如果当前多段线未被访问过，且起点接近当前中点，则继续查找
                                            if (!i.visit)
                                            {
                                                // 判断中点是否与多段线的起点接近
                                                if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(0).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(0).Y) < 0.1f)
                                                {
                                                    // 如果接近，则将多段线的终点加入到查找队列
                                                    findpoint.Enqueue(i.line.GetPoint3dAt(1));

                                                    // 设置当前路径的终点为多段线的终点
                                                    midtuple.end = i.line.GetPoint3dAt(1);

                                                    // 用于判断当前路径段是否与数字文本有关
                                                    bool pp = false;
                                                    foreach (DBText p in numtxt)
                                                    {
                                                        // 判断当前数字文本p的位置与路径线段的距离是否小于指定阈值wei_dis
                                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis /*&&IsPointAboveLineSegment(midtuple.start, midtuple.end, p.Position)*/)
                                                        {
                                                            // 如果接近，则将该文本的数字添加到wei列表中
                                                            wei.Add(p.TextString);
                                                            pp = true; // 设置标记为找到相关文本
                                                            break;// 找到第一个相关文本后跳出循环
                                                        }
                                                    }
                                                    // 如果没有找到相关文本，则添加“0”表示没有相关文本
                                                    if (!pp)
                                                        wei.Add("0");

                                                    // 将当前路径的起点和终点添加到tuple列表
                                                    tuple.Add(midtuple);

                                                    // 标记当前多段线为已访问
                                                    i.visit = true;

                                                    // 更新Polys列表中该多段线的状态
                                                    Polys[ij] = i;

                                                    // 增加路径查找的计数器
                                                    j++;

                                                    // 如果查找的路径次数达到6次，则停止进一步查找
                                                    if (j >= 6)
                                                    {
                                                        break;
                                                    }
                                                }
                                                // 如果当前中点接近多段线的终点，则反向查找
                                                else if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(1).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(1).Y) < 0.1f)
                                                {
                                                    // 将多段线的起点加入到查找队列
                                                    findpoint.Enqueue(i.line.GetPoint3dAt(0));
                                                    midtuple.end = i.line.GetPoint3dAt(0);
                                                    bool pp = false;
                                                    foreach (DBText p in numtxt)
                                                    {
                                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis /* && IsPointAboveLineSegment(midtuple.start, midtuple.end, p.Position)*/)
                                                        {
                                                            wei.Add(p.TextString);
                                                            pp = true;
                                                            break;
                                                        }
                                                    }
                                                    if (!pp)
                                                        wei.Add("0");
                                                    tuple.Add(midtuple);

                                                    i.visit = true;

                                                    Polys[ij] = i;
                                                    continue;

                                                }
                                            }
                                        }
                                    }

                                }//寻径电线
                        }
                        
                        File.Delete("tes.txt");

/*
                        // 遍历 Polys 列表
                        foreach (Poly i in Polys)
                        {
                            // 检查该多段线是否已被访问
                            if (i.visit)
                            {
                                // 确保多段线不为 null
                                if (i.line != null)
                                {
                                    // 使用 Teigha 的 Color 类设置为红色
                                    i.line.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);  // 红色：RGB(255, 0, 0)

                                }
                            }
                        }*/

                        //List<tuple> new_tuple = new List<tuple>();
                        //for (int o = 0; o < tuple.Count; o++)
                        //{
                        //    foreach(var i in Hatchs)
                        //    {
                        //        if(DistancePointToLineSegment(i, tuple[o].start, tuple[o].end)<0.1f)
                        //        {
                        //            Point3d midq=CalculateProjection(tuple[o].start, tuple[o].end,i);
                        //            tuple midtuple;
                        //            midtuple.start = tuple[o].start;
                        //            midtuple.end = midq;
                        //            new_tuple.Add(midtuple);
                        //            midtuple.start = midq;
                        //            midtuple.end = tuple[o].end;
                        //            new_tuple.Add(midtuple);
                        //            File.AppendAllText("tes.txt", "直线2：" + " " + midtuple.start.X + " " + midtuple.start.Y + " " + midtuple.end.X + " " + midtuple.end.Y + " " + "\r\n");
                        //            break;
                        //        }
                        //    }

                        //    //for (int ij = 0; ij < lines.Count; ij++)
                        //    //{
                        //    //    if (GetPointIsInLine(lines[ij].EndPoint, tuple[o].start, tuple[o].end, 0.04))
                        //    //    {
                        //    //        tuple midtuple;
                        //    //        midtuple.start = lines[ij].EndPoint;
                        //    //        midtuple.end = lines[ij].StartPoint;
                        //    //        new_tuple.Add(midtuple);
                        //    //        File.AppendAllText("tes.txt", "直线2：" + " " + midtuple.start.X + " " + midtuple.start.Y + " " + midtuple.end.X + " " + midtuple.end.Y + " " + "\r\n");
                        //    //    }
                        //    //}
                        //}




                        List<tuple3> tuple3 = new List<tuple3>();
                        Console.WriteLine($"tuple.Count: {tuple.Count}");
                        for (int o = 0; o < tuple.Count; o++)
                        {
                            tuple3 tuple7;
                            tuple7.start = tuple[o].start;
                            tuple7.end = tuple[o].end;
                            int myInt = int.Parse(wei[o]);
                            tuple7.weight = myInt;
                            tuple3.Add(tuple7);
                            File.AppendAllText("tes.txt", "zhixian1：" + tuple7.weight + " " + tuple7.start.X + " " + tuple7.start.Y + " " + tuple7.end.X + " " + tuple7.end.Y + " " + "\r\n");
                        }//中间三元组处理

                        WeightedUndirectedGraph gragh = new WeightedUndirectedGraph();
                        foreach (tuple3 i in tuple3)
                        {
                            int w = i.weight;
                            Point3d f = i.start;
                            Point3d t = i.end;
                            gragh.AddEdge(f, t, w);
                        }//初始化无向图 



                        stopwatch5.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time50: {stopwatch5.ElapsedMilliseconds} ms");


                        #endregion


                        //（修改！！）用户交互
                        #region 无向图顶点和接插件名称匹配




                        Stopwatch stopwatch6 = new Stopwatch();
                        stopwatch6.Start(); // 开始计时




                        List<Point3d> vetex = gragh.RE();

                        // 设定一个阈值，用于判断点是否靠近
                        double threshold = 1.0;

                        // 创建一个新的列表，用于存储靠近 Hatchs 的顶点
                        List<Point3d> filteredVetex = new List<Point3d>();

                        // 遍历 vetex 中的每个顶点
                        foreach (Point3d vertex in vetex)
                        {
                            bool isClose = false;

                            // 遍历 Hatchs 中的每个点，检查是否与 vetex 中的点距离小于等于阈值
                            foreach (Point3d hatchPoint in Hatchs)
                            {
                                // 计算 vetex 中点与 Hatchs 中点的距离
                                double distance = vertex.DistanceTo(hatchPoint);

                                // 如果距离小于等于阈值，标记为靠近
                                if (distance <= threshold)
                                {
                                    isClose = true;
                                    break; // 找到一个靠近的点后，立即退出内层循环
                                }
                            }

                            // 如果找到了靠近的点，则将 vertex 添加到 filteredVetex 列表中
                            if (isClose)
                            {
                                filteredVetex.Add(vertex);
                            }
                        }


                        // 遍历每个叶子节点，绘制一个圆，圆心为叶子节点，半径为 10
                        foreach (Point3d leaf in filteredVetex)
                        {
                            // 创建一个 Circle 对象，圆心为 leaf，半径为 10
                            Circle circle = new Circle(leaf, Vector3d.ZAxis, 5);
                            circle.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);
                            // 将圆添加到模型空间
                            modelSpace.AppendEntity(circle);

                            // 将圆实体添加到当前事务中
                            tr.AddNewlyCreatedDBObject(circle, true);
                        }

                        Dictionary<Point3d, DBText> closestPoints = new Dictionary<Point3d, DBText>();
                        List<Point3d> repo = new List<Point3d>();
                        Dictionary<Point3d, List<double>> dicc = new Dictionary<Point3d, List<double>>();
                        Dictionary<Point3d, double> dic = new Dictionary<Point3d, double>();
                        File.Delete("centers.txt");
                        File.Delete("n3.txt");
                        File.AppendAllText("n3.txt", vetex.Count + "\n" + vetextxt.Count + "\n");
                        foreach (DBText pttxt in vetextxt)
                        {
                            File.AppendAllText("n3.txt", vetextxt.Count + pttxt.TextString + "\n");
                            File.AppendAllText("centers.txt", pttxt.Position.X + "," + pttxt.Position.Y + "\n");
                        }

                        /*                        List<List<Point3d>> groupedCoordinates = new List<List<Point3d>>();
                        List<Point3d> kgroup = new List<Point3d>();
                        foreach (string line in File.ReadLines("grouped_elements_constrained.txt"))
                        {
                            string trimmedLine = line.Trim();
                            if (!string.IsNullOrEmpty(trimmedLine))
                            {
                                string[] parts = trimmedLine.Split(',');
                                double x = double.Parse(parts[0]);
                                double y = double.Parse(parts[1]);
                                kgroup.Add(new Point3d(x, y, 0));
                            }
                            else
                            {
                                if (kgroup.Count > 0)
                                {
                                    groupedCoordinates.Add(new List<Point3d>(kgroup));
                                    kgroup.Clear();
                                }
                            }
                        }

                        if (kgroup.Count > 0)
                        {
                            groupedCoordinates.Add(kgroup);
                        }

                        // 反查 Ap.add 和 vetextxt 中的 TextString
                        List<string> groupedTexts = new List<string>();

                        foreach (List<Point3d> groupCoords in groupedCoordinates)
                        {
                            foreach (Point3d coord in groupCoords)
                            {
                                string foundText = null;

                                // 在 Ap.add 中查找
                                foreach (Port i in Ap.add)
                                {
                                    if (Math.Abs(i.XS.Position.X - coord.X) < 0.001 && Math.Abs(i.XS.Position.Y - coord.Y) < 0.001)
                                    {
                                        foundText = i.XS.TextString;
                                        break;
                                    }
                                }

                                // 如果未找到，再在 vetextxt 中查找
                                if (foundText == null)
                                {
                                    foreach (DBText pttxt in vetextxt)
                                    {
                                        if (Math.Abs(pttxt.Position.X - coord.X) < 0.001 && Math.Abs(pttxt.Position.Y - coord.Y) < 0.001)
                                        {
                                            foundText = pttxt.TextString;
                                            break;
                                        }
                                    }
                                }

                                if (foundText != null)
                                {
                                    groupedTexts.Add(foundText);
                                }
                            }
                            groupedTexts.Add(""); // 添加空行分隔每一组
                        }

                        // 将分组文本写入新的文件
                        File.WriteAllLines("grouped_plus++_output.txt", groupedTexts);*/


                        foreach (Point3d pt in filteredVetex)
                        {
                            double minDist = double.MaxValue; // 当前点到最近文本的最小距离
                            DBText closestPt = new DBText();  // 当前点对应的最近文本
                            List<KeyValuePair<DBText, double>> textDistanceList = new List<KeyValuePair<DBText, double>>();
                            //将点与最近的文本进行匹配
                            foreach (DBText pttxt in vetextxt)
                            {
                                //double dist = Math.Sqrt(Math.Pow(pttxt.Position.X - pt.X, 2) + Math.Pow(pttxt.Position.Y - pt.Y, 2));
                                double dist = pt.DistanceTo(pttxt.Position);
                                textDistanceList.Add(new KeyValuePair<DBText, double>(pttxt, dist));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    closestPt = pttxt;
                                }
                            }

                            dic.Add(pt, minDist);
                            //当找到的最近文本已经被匹配了的处理
                            //检查closestPoints字典中是否已经包含了当前找到的最近文本注释closestPt。
                            if (closestPoints.ContainsValue(closestPt))
                            {
                                //获取所有与closestPt匹配的点（即Point3d）
                                List<Point3d> keys = closestPoints.GetKeysByValue(closestPt);
                                //比较当前点到closestPt的距离minDist与已存在点（keys[0]）到closestPt的距离dic[keys[0]]。
                                if (dic[keys[0]] > minDist)
                                {
                                    //如果当前点的距离更小（即更近），则移除原有的匹配并替换成当前点
                                    closestPoints.Remove(keys[0]);
                                    repo.Add(keys[0]);
                                    closestPoints.Add(pt, closestPt);

                                }
                                else
                                {
                                    //否则，将当前点添加到未匹配列表repo中
                                    repo.Add(pt);

                                }

                            }
                            else
                            {
                                //如果closestPt没有被其他点匹配，直接将当前点（pt）与closestPt添加到closestPoints字典中。
                                closestPoints.Add(pt, closestPt);

                            }

                            ////默认第二次正确
                            //if (closestPoints.ContainsValue(closestPt))
                            //{
                            //    List<Point3d> keys = closestPoints.GetKeysByValue(closestPt);
                            //    closestPoints.Remove(keys[0]);
                            //    repo.Add(keys[0]);
                            //}


                        }





                        File.AppendAllText("ttc.txt", "有端点的接插件：" + " " + closestPoints.Count() + "\r\n");
                        foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                        {
                            File.AppendAllText("ttc.txt", "有这些：" + " " + i.Value.TextString + " " + i.Key.X + " " + i.Key.Y + "\r\n");
                        }
                        File.Delete("未匹配.txt");
                        //添加遗漏线束
                        Encoding encoding = Encoding.Default;

                        List<DBText> remainingTexts = new List<DBText>();

                        // 遍历所有文本对象
                        foreach (DBText text in vetextxt)
                        {
                            // 检查该文本是否已经被匹配
                            bool isMatched = closestPoints.ContainsValue(text);

                            // 如果文本没有被匹配，将其添加到 remainingTexts 列表中
                            if (!isMatched)
                            {
                                remainingTexts.Add(text);
                            }
                        }

                        // 遍历剩余未匹配的文本
                        foreach (DBText text in remainingTexts)
                        {
                            double minDist = double.MaxValue;
                            Point3d closestPoint = Point3d.Origin;

                            // 遍历repo中未匹配的点，找到与当前文本距离最近的点
                            foreach (Point3d pt in repo)
                            {
                                double dist = Math.Sqrt(Math.Pow(text.Position.X - pt.X, 2) + Math.Pow(text.Position.Y - pt.Y, 2));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    closestPoint = pt;
                                }
                            }

                            // 如果找到最接近的点，将其匹配到当前文本
                            if (closestPoint != Point3d.Origin)
                            {
                                closestPoints.Add(closestPoint, text);
                                repo.Remove(closestPoint);  // 已匹配的点从repo中移除
                            }
                        }


                        // 遍历字典中的每个键值对
                        foreach (var entry in closestPoints)
                        {
                            Point3d point = entry.Key; // 键：Point3d
                            DBText text = entry.Value; // 值：DBText

                            // 获取文本的位置
                            Point3d textPosition = text.Position;

                            // 创建一个从点到文本位置的线段
                            Line line = new Line(point, textPosition);
                            line.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1); // 设置为红色


                            // 将线段添加到模型空间
                            modelSpace.AppendEntity(line);
                            tr.AddNewlyCreatedDBObject(line, true);
                        }

                        foreach (Point3d leaf in repo)
                        {
                            // 创建一个 Circle 对象，圆心为 leaf，半径为 10
                            Circle circle = new Circle(leaf, Vector3d.ZAxis, 5);
                            circle.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 3);
                            // 将圆添加到模型空间
                            modelSpace.AppendEntity(circle);

                            // 将圆实体添加到当前事务中
                            tr.AddNewlyCreatedDBObject(circle, true);
                        }

                        List<DBText> weiszh = new List<DBText>();
                        foreach (DBText pttxt in vetextxt)
                        {
                            if (!closestPoints.ContainsValue(pttxt))//已经有的不考虑
                            {

                                File.AppendAllText("未匹配.txt", "未匹配的接插件：" + pttxt.TextString + " " + pttxt.Position.X + " " + pttxt.Position.Y + " " + "\n", encoding);
                                weiszh.Add(pttxt);
                            }
                        }
                        //手动匹配和用户交互：
                        if (lpipei != null)
                        {
                            foreach (Teigha_Learn_Cad.Form1.gpipei gp in TeighaBase.lpipei)
                            {
                                DBText closestPt = new DBText();
                                Point3d pt = new Point3d();
                                foreach (var i in weiszh)
                                {
                                    if (gp.jiechajian.Substring(8) == i.TextString)
                                    {
                                        closestPt = i;
                                        break;
                                    }
                                }
                                foreach (var i in repo)
                                {
                                    string fio = string.Format("{0} {1}", i.X, i.Y);
                                    if (gp.daundian.Substring(7) == fio)
                                    {
                                        pt = i;
                                        break;

                                    }
                                }
                                closestPoints.Add(pt, closestPt);
                                //File.AppendAllText("未匹配.txt", "匹配上"+ "\n",encoding);
                            }
                        }
                        //用户交互添加接插件
                        //for(int i=0;i< weiszh.Count; i++)
                        //{
                        //    closestPoints.Add(repo[4-i], weiszh[i]);
                        //}


                        //Point3d user_pt = new Point3d();
                        //DBText user_db = new DBText();
                        //closestPoints.Add(user_pt, user_db);



                        stopwatch6.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time60: {stopwatch6.ElapsedMilliseconds} ms");





                        #endregion

                        //处理未匹配的端点和接插件                
                        /*                        DBText closest_jiechajian = new DBText();
                                                Point3d closest_point = new Point3d();
                                                List<Point3d> matched_point = new List<Point3d>();
                                                foreach (var i in weiszh)
                                                {
                                                    double mindist = double.MaxValue;
                                                    foreach (var j in repo)
                                                    {
                                                        double dist = Math.Sqrt(Math.Pow(i.Position.X - j.X, 2) + Math.Pow(i.Position.Y - j.Y, 2));
                                                        if(dist < mindist)
                                                        {
                                                            mindist = dist;
                                                            closest_point = j;
                                                        }
                                                    }
                                                    if(!closestPoints.ContainsValue(i) && !matched_point.Contains(closest_point))
                                                    {
                                                        closestPoints.Add(closest_point, i);
                                                    }
                                                }*/


                        #region 对主保险丝盒端点和接插件名称的特化，因为不在顶点,可不用管
                        /*                        foreach (var i in weiszh)
                                                {
                                                    if(i.TextString == "主保险丝盒总成")
                                                    {
                                                        foreach (var j in repo)
                                                        {
                                                            //这里的位置需要进行处理后面
                                                            if(j.X > 1990 && j.X < 2010)
                                                            {
                                                                //修改接插件名称
                                                                DBText newText = new DBText
                                                                {
                                                                    TextString = "主保险丝盒总成BLOCK_ASSY__MAIN_FUSE",
                                                                    Position = i.Position,  // 保留原始位置
                                                                    TextStyleId = i.TextStyleId,
                                                                    Layer = i.Layer,
                                                                    Color = i.Color,
                                                                    Height = i.Height,
                                                                    AlignmentPoint = i.AlignmentPoint,
                                                                    HorizontalMode = i.HorizontalMode,
                                                                    VerticalMode = i.VerticalMode,
                                                                    Rotation = i.Rotation,
                                                                    Oblique = i.Oblique,
                                                                    WidthFactor = i.WidthFactor,
                                                                    IsMirroredInX = i.IsMirroredInX,
                                                                    IsMirroredInY = i.IsMirroredInY,
                                                                    Thickness = i.Thickness
                                                                };
                                                                closestPoints.Add(j, newText);
                                                            }
                                                        }
                                                    }
                                                }*/
                        //初始化两个 Point3d 类型的变量，分别用来存储新的和旧的保险丝盒位置。
                        /*                        Point3d bxsh = new Point3d();
                                                Point3d oldbxsh = new Point3d();
                                                //初始化一个 DBText 类型的变量，用来临时存储与保险丝盒相关的文本注释。
                                                DBText bxshtxt = new DBText();
                                                //循环遍历 closestPoints 字典中的每一个键值对（每个点与其对应的文本注释）。
                                                foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                                                {
                                                    //检查每个文本注释的 TextString 属性是否等于 "主保险丝盒总成"。
                                                    if (i.Value.TextString == "主保险丝盒总成")
                                                    {
                                                        //如果 X 坐标大于 2000，将当前点存储在 oldbxsh 变量中，并将文本注释存储在 bxshtxt。
                                                        if (i.Value.Position.X > 2000)
                                                        {
                                                            oldbxsh = i.Key;
                                                            bxshtxt = i.Value;
                                                        }
                                                        //调用 gragh.Neighbor(i.Key) 方法获取当前点的邻近点，并存储在 bxsh 变量中。
                                                        else
                                                        {
                                                            bxsh = gragh.Neighbor(i.Key);
                                                        }
                                                    }


                                                }
                                                //检查 bxsh 的 X 坐标是否不为 0（即已经为 bxsh 赋值了新的点），这意味着找到了新的合适位置用于替换旧位置。
                                                //移除信息，添加新的信息
                                                if (bxsh.X != 0)
                                                {
                                                    closestPoints.Remove(oldbxsh);
                                                    closestPoints.Add(bxsh, bxshtxt);
                                                }*/
                        #endregion





                        #region  
                        /*                        File.Delete("jiecahjian.txt");*/
                        //定义一个字典，其键为端口列表 (List<Port>)，值为文本注释 (DBText)。
                        Dictionary<List<Port>, DBText> jiechajian = new Dictionary<List<Port>, DBText>();
                        Dictionary<List<Port>, DBText> biaogejiechajian = new Dictionary<List<Port>, DBText>();
                        //遍历 closestPoints 字典中的每个键值对。每个键是一个 Point3d 对象，表示点的位置；每个值是一个 DBText 对象，表示与该点相关联的文本注释。
                        //先将保险丝盒与端口进行关联
                        /*foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                        {
                            if(i.Value.TextString == "主保险丝盒总成BLOCK_ASSY__MAIN_FUSE" && i.Value.Position.X > 2000)
                            {
                                jiechajian.Add(ppp[0], i.Value);
                            }
                        }*/
                        File.Delete("jiechajian.txt");
                        File.Delete("biaogejiechajian.txt");

                        foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                        {
                            Point3d p = i.Value.Position;
                            double minD = double.MaxValue;
                            //用于记录与当前点最近距离的端口列表
                            List<Port> po = new List<Port>();
                            foreach (List<Port> j in ppp)//AP2换ppp
                            {
                                //记录当前找到的最小距离，初始化为最大可能的双精度浮点数。
                                double minDist = double.MaxValue;
                                //遍历列表中的每个端口 u，计算其位置 u.XS.Position 与点 p 之间的欧氏距离 dist。
                                foreach (Port u in j)
                                {
                                    double dist = Math.Sqrt(Math.Pow(u.XS.Position.X - p.X, 2) + Math.Pow(u.XS.Position.Y - p.Y, 2));
                                    if (dist < minDist)
                                        minDist = dist;
                                }
                                //如果找到的最小距离 minDist 小于之前记录的 minD，更新 minD 并记录当前端口列表 po 作为最近的端口列表。
                                if (minDist < minD)
                                {
                                    minD = minDist;
                                    po = j;
                                }
                            }

                            if (jiechajian.ContainsKey(po))
                            {
                                File.AppendAllText("tt.txt", "因重复没有接插件的端口：" + po.Count + " " + po[0].XS.TextString /*+" " + po[1].XS.TextString */+ " " + i.Value.TextString + " " + i.Value.Position.X + "  " + i.Value.Position.Y + "\r\n");
                            }
                            else
                            {
                                //将 po 和对应的 DBText 加入到 jiechajian。
                                jiechajian.Add(po, i.Value);
                                File.AppendAllText("jiechajian.txt", "接插件：" + i.Value.TextString + "\n");
                            }

                        }
                        foreach (DBText pttxt in biaogevetextxt)
                        {
                            Point3d p = pttxt.Position;
                            //double minD = double.MaxValue;
                            List<Port> po = new List<Port>();
                            int maxCloserCount = 0;
                            foreach (List<Port> j in Ap2)
                            {
                                //double minDist = double.MaxValue;
                                int closerCount = 0;
                                int fartherCount = 0;

                                foreach (Port u in j)
                                {
                                    double dist = Math.Sqrt(Math.Pow(u.XS.Position.X - p.X, 2) + Math.Pow(u.XS.Position.Y - p.Y, 2));
                                    if (dist < 55)
                                    {
                                        bool isCloser = true;

                                        foreach (DBText otherPttxt in biaogevetextxt)
                                        {
                                            if (otherPttxt == pttxt) continue; // 跳过当前的p

                                            Point3d otherP = otherPttxt.Position;
                                            double otherDist = Math.Sqrt(Math.Pow(u.XS.Position.X - otherP.X, 2) + Math.Pow(u.XS.Position.Y - otherP.Y, 2));

                                            if (otherDist < dist)
                                            {
                                                isCloser = false;
                                                break;
                                            }
                                        }

                                        if (isCloser)
                                        {
                                            closerCount++;
                                        }
                                        else
                                        {
                                            fartherCount++;
                                        }
                                    }
                                    //   if (dist < minDist)
                                    //       minDist = dist;
                                }
                                if (closerCount > fartherCount && closerCount > maxCloserCount)
                                {
                                    maxCloserCount = closerCount;
                                    po = j;
                                }
                                /*if (minDist < minD)
                                {
                                    minD = minDist;
                                    po = j;
                                }*/
                            }
                            File.AppendAllText("biaogejiechajian.txt", "表格接插件：" + pttxt.TextString + "\r\n");

                            foreach (Port i in po)
                            {
                                File.AppendAllText("biaogejiechajian.txt", "线束名：" + i.XS.TextString + " " + "\r\n");
                                File.AppendAllText("biaogejiechajian.txt", "端口：" + i.port.TextString + " " + "\r\n");
                                File.AppendAllText("biaogejiechajian.txt", "颜色：" + i.color.TextString + " " + "\r\n");
                                File.AppendAllText("biaogejiechajian.txt", "线宽：" + i.width.TextString + " " + "\r\n\n");
                            }
                            if (biaogejiechajian.ContainsKey(po))
                            {
                                File.AppendAllText("biaogejiechajian.txt", "因重复没有接插件的端口：" + po.Count + " " + po[0].XS.TextString + " " + pttxt.TextString + " " + "\r\n\n");
                            }
                            else
                            {
                                biaogejiechajian.Add(po, pttxt);
                            }

                        }
                        File.AppendAllText("tt.txt", "有端口接插件数量：" + jiechajian.Count() + "\r\n");
                        #endregion



                        /*#region 因为端口跨距太大对主配电盒的特化，不用管
                        //jiechajiankeys：从 jiechajian 字典中提取所有键（即端口列表）并转换为列表
                        var jiechajiankeys = jiechajian.Keys.ToList();
                        //始化一个空的 List<Port>，用于存储特定条件下的端口列表。
                        List<Port> popo = new List<Port>();
                        //初始化一个空的 DBText 对象，用于存储与 popo 关联的文本注释。
                        DBText poi = new DBText();
                        //遍历名为 ppp 的端口列表集合，查找文本标签为 "P03" 的端口列表，并将其存储在 popo
                        foreach (List<Port> j in ppp)
                        {

                            if (j[0].XS.TextString == "P03")
                            {
                                popo = j;
                            }

                        }
                        //遍历 jiechajiankeys，即之前从 jiechajian 中提取的端口列表的键集合。
                        foreach (var key in jiechajiankeys)
                        {
                            //检查与该键关联的 DBText 对象的 TextString 是否为 "主配电盒总成"，并且该文本的 X 坐标与 2208.5521 的差的绝对值小于 1f。
                            if (jiechajian[key].TextString == "主配电盒总成" && Math.Abs(jiechajian[key].Position.X - 2208.5521) < 1f)
                            {
                                //将该 DBText 对象存储在 poi
                                poi = jiechajian[key];
                                //检查端口列表是否非空，如果非空
                                if (key.Count > 0)
                                {
                                    //将该端口列表的第一个端口添加到 popo
                                    popo.Add(key[0]);
                                    //从 jiechajian 字典中移除当前的键值对。
                                    jiechajian.Remove(key);
                                    //将更新后的 popo 和 poi 作为新的键值对添加到 jiechajian 字典中。
                                    jiechajian.Add(popo, poi);
                                    //从 ppp 集合中移除当前的端口列表。
                                    ppp.Remove(key);
                                }

                            }
                        }
                        #endregion*/

                        /*#region 对主保险丝盒端口的特化，不用管
                        //bxsport: 初始化一个空的 List<Port> 对象，用来存储特定条件下的端口列表。
                        List<Port> bxsport = new List<Port>();
                        //bxsop: 初始化一个空的 DBText 对象，用于存储与 bxsport 相关联的文本注释。
                        DBText bxsop = new DBText();
                        foreach (KeyValuePair<List<Port>, DBText> i in jiechajian)//输出检测
                        {
                            //如果字典中的值（DBText 对象）的 TextString 是 "主保险丝盒总成" 并且其位置的 X 坐标大于 2000，则将这个 DBText 对象赋值给 bxsop。
                            if (i.Value.TextString == "主保险丝盒总成" && i.Value.Position.X > 2000)
                            {
                                bxsop = i.Value;
                            }
                        }
                        jiechajian.Remove(bxsport);
                        //遍历 ppp 集合中的每个端口列表 j
                        foreach (List<Port> j in ppp)
                        {
                            //如果 jiechajian 字典中不包含当前端口列表 j 作为键，则将 j 中的所有端口添加到 bxsport。
                            if (!jiechajian.ContainsKey(j))
                            {
                                bxsport.AddRange(j);
                            }

                        }
                        //检查 bxsop 的位置 X 坐标是否不为 0（确保 bxsop 已被正确赋值）：
                        if (bxsop.Position.X != 0)
                            jiechajian.Add(bxsport, bxsop);
                        #endregion*/

                        #region 把端口、接插件、无向图识别点结合


                        Stopwatch stopwatch7 = new Stopwatch();
                        stopwatch7.Start(); // 开始计时





                        List<Port_jc> pjc = new List<Port_jc>();
                        foreach (KeyValuePair<List<Port>, DBText> i in jiechajian)//输出检测
                        {
                            File.AppendAllText("tt.txt", "文字：" + i.Value.TextString + " " + i.Value.Position.X + " " + i.Value.Position.Y + " " + "\r\n");
                            KeyValuePair<Point3d, DBText> iip = new KeyValuePair<Point3d, DBText>();
                            foreach (KeyValuePair<Point3d, DBText> iii in closestPoints)
                            {
                                if (iii.Value == i.Value)
                                {
                                    File.AppendAllText("tt.txt", "文字：" + iii.Key.X + " " + iii.Key.Y + " " + "\r\n");
                                    iip = iii;
                                }

                            }
                            foreach (Port j in i.Key)
                            {
                                File.AppendAllText("tt.txt", "线束名：" + j.XS.TextString + " " + j.XS.Position.X + " " + j.XS.Position.Y + " " + "\r\n");
                                Port_jc opo = new Port_jc(iip.Value, j, iip.Key);
                                pjc.Add(opo);
                            }
                            File.AppendAllText("tt.txt", "\n\n");
                        }




                        File.Delete("test.txt");
                        foreach (var pop in pjc)
                        {
                            File.AppendAllText("test.txt", "线束标号：" + pop.pot.XS.TextString + "\r\n");
                            File.AppendAllText("test.txt", "接插件名称及接口：" + pop.jc.TextString + " " + pop.pot.port.TextString + "\r\n");
                            File.AppendAllText("test.txt", "线色和线径：" + pop.pot.color.TextString + " " + pop.pot.width.TextString + "\r\n");
                            File.AppendAllText("test.txt", "寻径点：" + pop.vet.X + " " + pop.vet.Y + "\r\n");
                            File.AppendAllText("test.txt", "\n\n");
                        }


                        stopwatch7.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time70: {stopwatch7.ElapsedMilliseconds} ms");



                        #endregion

                        foreach (Port_jc pop in pjc)
                        {
                            // 获取三个点
                            Point3d point1 = pop.pot.XS.Position; // pop.pot.XS.TextString
                            Point3d point2 = pop.vet;          // pop.vet
                            Point3d point3 = pop.jc.Position;  // pop.jc.TextString 通过 jc 获取一个坐标

                            Line line1 = new Line(point1, point2);
                            Line line2 = new Line(point1, point3);
                            line1.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);
                            line2.Color = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, 1);
                            modelSpace.AppendEntity(line1);
                            modelSpace.AppendEntity(line2);
                            tr.AddNewlyCreatedDBObject(line1, true);
                            tr.AddNewlyCreatedDBObject(line2, true);
                        }


                        #region 根据端口名分组，成员数为2的寻找各自接插件信息(换了AP）


                        Stopwatch stopwatch8 = new Stopwatch();
                        stopwatch8.Start(); // 开始计时






                        File.Delete("text.txt");

                        //List<Portsc> sc = new List<Portsc>();这里没有进入循环

                        var oogroup = ooo.GroupBy(p => p.XS.TextString);//AP3换ooo
                        List<Point3d> biaogeepoint = new List<Point3d>();

                        foreach (List<Port> j in Ap2)
                        {
                            Port startPort = new Port();
                            HashSet<Port> endPorts = new HashSet<Port>();

                            foreach (Port u in j)
                            {
                                string text = u.XS.TextString;

                                // 判断是否以数字结尾
                                bool endsWithNumber = char.IsDigit(text.Last());
                                string baseString = endsWithNumber ? text : text.Substring(0, text.Length - 1);

                                foreach (var pop in pjc)
                                {
                                    if (pop.pot.XS.TextString.StartsWith(baseString))
                                    {
                                        if (endsWithNumber)
                                        {
                                            // 以数字结尾的作为起点接口
                                            startPort = u;
                                        }
                                        else
                                        {
                                            // 以字母结尾的作为终点接口
                                            endPorts.Add(u);
                                        }
                                    }
                                }
                            }
                            // 检查是否所有的u都以字母结尾
                            bool allEndWithLetter = j.All(u => !char.IsDigit(u.XS.TextString.Last()));

                            // 如果所有的u都以字母结尾
                            if (allEndWithLetter)
                            {
                                // 检查线宽是否都相等
                                bool allWidthsEqual = j.Select(u => u.width.TextString).Distinct().Count() == 1;

                                if (allWidthsEqual)
                                {
                                    // 线宽都相等，按顺序第一个u作为起点，其他作为终点
                                    startPort = j.First();
                                    endPorts = new HashSet<Port>(j.Skip(1));
                                }
                                else
                                {
                                    // 线宽不等，找到线宽最大的u作为起点
                                    Port maxWidthPort = j.OrderByDescending(u => Convert.ToDouble(u.width.TextString)).FirstOrDefault();

                                    // 将最大线宽的u设为起点
                                    startPort = maxWidthPort;

                                    // 其他的u作为终点
                                    endPorts = new HashSet<Port>(j.Where(u => u.XS.TextString != maxWidthPort.XS.TextString));
                                }
                            }

                            // 如果找到起点和至少一个终点
                            if (startPort.XS != null && startPort.XS.TextString != null && endPorts.Count > 0)
                            {
                                foreach (Port endPort in endPorts)
                                {
                                    // 清空之前的点列表
                                    biaogeepoint.Clear();

                                    foreach (var pop in pjc)
                                    {
                                        if (pop.pot.XS.TextString == startPort.XS.TextString || pop.pot.XS.TextString == endPort.XS.TextString)
                                        {
                                            biaogeepoint.Add(pop.vet);
                                        }
                                    }

                                    File.AppendAllText("text.txt", "线束标号：" + endPort.XS.TextString + " " + "\r\n");
                                    File.AppendAllText("text.txt", "线色和线径：" + endPort.color.TextString + " " + endPort.width.TextString + "\r\n");

                                    // 找到起点和终点对应的vet并添加到列表
                                    int qidian = 1;
                                    int zhongdian = 1;
                                    foreach (var pop in pjc)
                                    {
                                        if (pop.pot.XS.TextString == startPort.XS.TextString)
                                        {
                                            qidian = 0;
                                            File.AppendAllText("text.txt", "起点接口：" + pop.jc.TextString + "/PIN" + pop.pot.port.TextString + "\r\n");
                                        }
                                        else if (pop.pot.XS.TextString == endPort.XS.TextString)
                                        {
                                            zhongdian = 0;
                                            File.AppendAllText("text.txt", "终点接口：" + pop.jc.TextString + "/PIN" + pop.pot.port.TextString + "\r\n");
                                        }

                                    }
                                    if (qidian == 1)
                                    {
                                        qidian = 1;
                                        File.AppendAllText("text.txt", "起点接口：" + "\r\n");
                                    }
                                    if (zhongdian == 1)
                                    {
                                        zhongdian = 1;
                                        File.AppendAllText("text.txt", "终点接口：" + "\r\n");
                                    }

                                    // 计算长度
                                    int le = 0;
                                    if (biaogeepoint.Count == 2)
                                    {
                                        le = gragh.DFS(biaogeepoint[0], biaogeepoint[1]);
                                    }

                                    // 写入长度信息
                                    File.AppendAllText("text.txt", "线束长度：" + le + "\r\n");
                                }
                            }
                        }

                        File.Delete("group.txt");
                        foreach (var group in oogroup)
                        {
                            File.AppendAllText("group.txt", "KEY:" + group.Key + "\r\n");
                            foreach (var item in group)
                            {
                                File.AppendAllText("group.txt", "XS:" + item.XS.TextString + "\r\n");
                            }

                            string thistext = group.Key;//线束名
                                                        //if (group.Count() != 2)
                                                        //{
                                                        //    foreach (var pot in group)
                                                        //    {
                                                        //        File.AppendAllText("test.txt", "线束名：" + pot.XS.TextString + " " + pot.XS.Position.X + " " + pot.XS.Position.Y + " " + "\r\n");
                                                        //    }
                                                        //    File.AppendAllText("test.txt", "\n\n");
                                                        //}
                            if (group.Count() == 2)
                            {
                                List<Point3d> epoint = new List<Point3d>();
                                File.AppendAllText("text.txt", "线束标号：" + thistext + " " + "\r\n");
                                File.AppendAllText("text.txt", "线色和线径：" + group.First().color.TextString + " " + group.First().width.TextString + "\r\n");
                                //File.AppendAllText("text.txt", "\n");
                                int ijishi = 0;
                                int flag = 0;
                                DBText linshijiecha = new DBText();
                                List<string> jie1 = new List<string>();
                                foreach (var pot in group)
                                {
                                    /*                                    if (ijishi == 0)
                                                                            File.AppendAllText("text.txt", "起点接口：");
                                                                        else
                                                                            File.AppendAllText("text.txt", "终点接口：");*/
                                    foreach (var pop in pjc)//输出检测
                                    {

                                        if (pop.pot.XS == pot.XS)
                                        {
                                            if (flag == 0)
                                            {
                                                File.AppendAllText("text.txt", "起点接口：");
                                            }
                                            else if (flag == 1)
                                            {
                                                File.AppendAllText("text.txt", "终点接口：");
                                            }
                                            File.AppendAllText("text.txt", pop.jc.TextString + "/PIN" + pop.pot.port.TextString + "\r\n");
                                            jie1.Add(pop.jc.TextString);
                                            //File.AppendAllText("text.txt", "线色和线径：" + pop.pot.color.TextString + " " + pop.pot.width.TextString + "\r\n");
                                            //File.AppendAllText("text.txt", "寻径点：" + pop.vet.X + " " + pop.vet.Y + "\r\n");
                                            epoint.Add(pop.vet);
                                            linshijiecha = pop.jc;
                                            flag++;
                                        }
                                    }
                                    //File.AppendAllText("text.txt", "\r\n");
                                    ijishi++;
                                }
                                if (flag == 0)
                                {
                                    File.AppendAllText("text.txt", "起点接口：\r\n");
                                    File.AppendAllText("text.txt", "终点接口：\r\n");
                                }
                                if (flag == 1)
                                {
                                    File.AppendAllText("text.txt", "终点接口：\r\n");
                                }
                                int le = 0;
                                if (epoint.Count == 2)
                                    le = gragh.DFS(epoint[0], epoint[1]);
                                //Portsc ls = new Portsc(group.First(), group.Last(), le, linshijiecha);
                                //sc.Add(ls);//按照线束为单位保存，没有用上
                                File.AppendAllText("text.txt", "线束长度：" + le + "\r\n");
                                //File.AppendAllText("text.txt", "\n\n");
                                if (thistext == sq)
                                {
                                    // 指定要写入的文件路径
                                    string filePath = "points.txt";
                                    // 使用StreamWriter以追加模式写入文件
                                    using (StreamWriter writer = new StreamWriter(filePath, true))
                                    {
                                        writer.WriteLine($"开始图纸分析");
                                    }
                                    List<Point3d> Apo = gragh.DFSWithPath(epoint[0], epoint[1]);
                                    for (int ij = 0; ij < Polylines.Count; ij++)
                                    {
                                        Teigha.DatabaseServices.Polyline i = Polylines[ij];
                                        if (Apo.Contains(i.GetPoint3dAt(0)) && Apo.Contains(i.GetPoint3dAt(1)))
                                        {
                                            //i.Highlight();
                                            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
                                            {
                                                // 使用 'i' 中的前两个点创建一条新线
                                                Line line = new Line(i.GetPoint3dAt(0), i.GetPoint3dAt(1));
                                                // 将创建的线设置为临时实体
                                                TemporaryEnt = line;
                                                // 按顺序选择颜色列表中的颜色
                                                Teigha.Colors.Color selectedColor = colors[colorIndex];
                                                //Teigha.Colors.Color selectedColor = Teigha.Colors.Color.FromRgb(255, 0, 0);
                                                // 设置线的颜色，使用选择的颜色
                                                TemporaryEnt.Color = selectedColor;
                                                // 设置线的宽度，例如设置为 2.0
                                                TemporaryEnt.LineWeight = Teigha.DatabaseServices.LineWeight.LineWeight050; // 20 (2.0 mm) 线宽
                                                //colorIndex = (colorIndex + 1) % colors.Count;
                                                // 将临时实体添加到活动视图
                                                pView.Add(TemporaryEnt);

                                            }

                                            // 使用StreamWriter以追加模式写入文件
                                            using (StreamWriter writer = new StreamWriter(filePath, true))
                                            {
                                                // 获取点的坐标
                                                Point3d point1 = i.GetPoint3dAt(0);
                                                Point3d point2 = i.GetPoint3dAt(1);

                                                // 写入文件的格式，包含循环索引
                                                writer.WriteLine($"StartPoint: X={point1.X}, Y={point1.Y}, Z={point1.Z}   EndPoint: X={point2.X}, Y={point2.Y}, Z={point2.Z}");
                                                //writer.WriteLine($"EndPoint: X={point2.X}, Y={point2.Y}, Z={point2.Z}");
                                            }

                                        }
                                    }
                                    for (int ioy = 0; ioy < txts.Count; ioy++)
                                    {
                                        if (txts[ioy].TextString == sq)
                                        {
                                            txts[ioy].Highlight();
                                            using (StreamWriter writer = new StreamWriter(filePath, true))
                                            {
                                                writer.WriteLine($"{txts[ioy].TextString} X={txts[ioy].Position.X},Y={txts[ioy].Position.Y},Z={0.0}");
                                            }
                                        }
                                        if (txts[ioy].TextString == jie1[0] || txts[ioy].TextString == jie1[1])
                                        {
                                            txts[ioy].Highlight();
                                            using (StreamWriter writer = new StreamWriter(filePath, true))
                                            {
                                                writer.WriteLine($"{txts[ioy].TextString} X={txts[ioy].Position.X},Y={txts[ioy].Position.Y},Z={0.0}");
                                            }
                                        }
                                    }
                                }
                            }

                            //if (group.Count() > 2)
                            //    Console.WriteLine("这些线束的端口超过两个");

                            //if (group.Count() == 1)
                            //    Console.WriteLine("这些线束的一端端口在中间件上，需要您自行决定走向");
                        }




                        stopwatch8.Stop(); // 停止计时
                        Console.WriteLine($"ModelSpace Execution Time80: {stopwatch8.ElapsedMilliseconds} ms");




                        #endregion

                        #region 输出中间件的端口
                        File.Delete("aaa.txt");
                        foreach (var i in iiom)
                        {
                            foreach (var j in i)
                            {
                                File.AppendAllText("aaa.txt", j.TextString + "\n");
                                foreach (var pop in pjc)
                                {
                                    if (pop.pot.XS.TextString == j.TextString)
                                    {
                                        File.AppendAllText("aaa.txt", "接插件名称及接口：" + pop.jc.TextString + " " + pop.pot.port.TextString + "\r\n");
                                        File.AppendAllText("aaa.txt", "线色和线径：" + pop.pot.color.TextString + " " + pop.pot.width.TextString + "\r\n");
                                        File.AppendAllText("aaa.txt", "寻径点：" + pop.vet.X + " " + pop.vet.Y + "\r\n");
                                    }
                                }
                                File.AppendAllText("aaa.txt", "\n");
                            }
                            File.AppendAllText("aaa.txt", "\n\n");
                        }
                        #endregion


                        // 更新颜色索引计数器，以便下次使用下一个颜色
                        colorIndex = (colorIndex + 1) % colors.Count;

                        //foreach (List<Port> j in ppp)测试未统计分组，主保险丝盒子
                        //{
                        //    if (!jiechajian.ContainsKey(j))
                        //    {
                        //        foreach (Port i in j)
                        //        {
                        //            File.AppendAllText("tt.txt", "线束名：" + i.XS.TextString + " " + i.XS.Position.X + " " + i.XS.Position.Y + " " + "\r\n");
                        //        }
                        //        File.AppendAllText("tt.txt", "\n\n");
                        //    }

                        //}


                        //foreach (KeyValuePair<Point3d, DBText> i in closestPoints)//输出检测
                        //{
                        //    File.AppendAllText("ttc.txt", "文字：" + i.Value.TextString + " " + i.Value.Position.X + " " + i.Value.Position.Y + " " + "\r\n" + i.Key.X + " " + i.Key.Y + "\r\n\n");
                        //}

                        //foreach (DBText pttxt in vetextxt)
                        //{
                        //    if (!closestPoints.ContainsValue(pttxt))
                        //        File.AppendAllText("ttc.txt", "没找到：" + pttxt.TextString + " " + pttxt.Position.X + " " + pttxt.Position.Y + " " + "\r\n");
                        //}


                        //无向图寻径测试
                        //File.Delete("ttc.txt");
                        //Point3d epoint = tuple3[0].end;
                        //Point3d tpoint = tuple3[100].end; 
                        //int le=gragh.DFS(epoint,tpoint);
                        //File.AppendAllText("ttc.txt", "文字：" + le+ " " + "\r\n");




                    }

                    // 提交事务
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                string error = $"程序异常，请联系管理人员：{ex.Message}\r\n{ex.StackTrace}";
                MessageBox.Show(error);
                foreach (var control in this.Controls)
                {
                    (control as Control).Enabled = false;
                }
            }







            stopwatch.Stop();
            Console.WriteLine($"Execution Time1: {stopwatch.ElapsedMilliseconds} ms");










        }

        private HashSet<Line> FindConnectedLines(Line startLine, List<Line> allLines)
        {
            if (allLines == null || allLines.Count == 0) return new HashSet<Line>();

            // 计算所有线段的边界
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var line in allLines)
            {
                var extents = line.GeometricExtents;
                minX = Math.Min(minX, extents.MinPoint.X);
                minY = Math.Min(minY, extents.MinPoint.Y);
                maxX = Math.Max(maxX, extents.MaxPoint.X);
                maxY = Math.Max(maxY, extents.MaxPoint.Y);
            }

            // 设置网格单元大小（可根据数据集调整）
            double cellSize = 10.0;

            // 构建网格
            var grid = new Dictionary<(int, int), List<Line>>();
            foreach (var line in allLines)
            {
                // 计算线段相交的网格单元范围
                double lineMinX = Math.Min(line.StartPoint.X, line.EndPoint.X);
                double lineMaxX = Math.Max(line.StartPoint.X, line.EndPoint.X);
                double lineMinY = Math.Min(line.StartPoint.Y, line.EndPoint.Y);
                double lineMaxY = Math.Max(line.StartPoint.Y, line.EndPoint.Y);

                int iMin = (int)Math.Floor((lineMinX - minX) / cellSize);
                int iMax = (int)Math.Floor((lineMaxX - minX) / cellSize);
                int jMin = (int)Math.Floor((lineMinY - minY) / cellSize);
                int jMax = (int)Math.Floor((lineMaxY - minY) / cellSize);

                // 将线段添加到所有相交的网格单元
                for (int i = iMin; i <= iMax; i++)
                {
                    for (int j = jMin; j <= jMax; j++)
                    {
                        var key = (i, j);
                        if (!grid.ContainsKey(key))
                            grid[key] = new List<Line>();
                        if (!grid[key].Contains(line))
                            grid[key].Add(line);
                    }
                }
            }

            // 执行 BFS，使用网格索引
            Queue<Line> queue = new Queue<Line>();
            HashSet<Line> visited = new HashSet<Line>();
            queue.Enqueue(startLine);
            visited.Add(startLine);

            while (queue.Count > 0)
            {
                Line current = queue.Dequeue();

                // 计算当前线段相交的网格单元范围
                double currentMinX = Math.Min(current.StartPoint.X, current.EndPoint.X);
                double currentMaxX = Math.Max(current.StartPoint.X, current.EndPoint.X);
                double currentMinY = Math.Min(current.StartPoint.Y, current.EndPoint.Y);
                double currentMaxY = Math.Max(current.StartPoint.Y, current.EndPoint.Y);

                int currentIMin = (int)Math.Floor((currentMinX - minX) / cellSize);
                int currentIMax = (int)Math.Floor((currentMaxX - minX) / cellSize);
                int currentJMin = (int)Math.Floor((currentMinY - minY) / cellSize);
                int currentJMax = (int)Math.Floor((currentMaxY - minY) / cellSize);

                // 从相交的网格单元收集候选线段
                HashSet<Line> candidates = new HashSet<Line>();
                for (int i = currentIMin; i <= currentIMax; i++)
                {
                    for (int j = currentJMin; j <= currentJMax; j++)
                    {
                        var key = (i, j);
                        if (grid.ContainsKey(key))
                        {
                            foreach (var other in grid[key])
                            {
                                candidates.Add(other);
                            }
                        }
                    }
                }

                // 检查候选线段的连接关系
                foreach (var other in candidates)
                {
                    if (visited.Contains(other)) continue;
                    if (IsIntersecting(current, other) || IsTouching(current, other) || IsTConnection(current, other))
                    {
                        queue.Enqueue(other);
                        visited.Add(other);
                    }
                }
            }

            return visited;
        }

        // 判断两条 Line 是否相交
        private bool IsIntersecting(Line line1, Line line2)
        {
            Teigha.Geometry.Point3d A = line1.StartPoint;
            Teigha.Geometry.Point3d B = line1.EndPoint;
            Teigha.Geometry.Point3d C = line2.StartPoint;
            Teigha.Geometry.Point3d D = line2.EndPoint;

            // Quick rejection test using bounding boxes
            if (Math.Max(A.X, B.X) < Math.Min(C.X, D.X) ||
                Math.Max(C.X, D.X) < Math.Min(A.X, B.X) ||
                Math.Max(A.Y, B.Y) < Math.Min(C.Y, D.Y) ||
                Math.Max(C.Y, D.Y) < Math.Min(A.Y, B.Y))
            {
                return false;
            }

            // Straddle test using cross products
            double cross1 = (B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X);
            double cross2 = (B.X - A.X) * (D.Y - A.Y) - (B.Y - A.Y) * (D.X - A.X);
            double cross3 = (D.X - C.X) * (A.Y - C.Y) - (D.Y - C.Y) * (A.X - C.X);
            double cross4 = (D.X - C.X) * (B.Y - C.Y) - (D.Y - C.Y) * (B.X - C.X);

            if (cross1 * cross2 < 0 && cross3 * cross4 < 0)
            {
                return true;
            }

            return false;
        }

        // 判断两条 Line 是否在端点相接
        private bool IsTouching(Line line1, Line line2)
        {
            return line1.StartPoint == line2.StartPoint || line1.StartPoint == line2.EndPoint ||
                   line1.EndPoint == line2.StartPoint || line1.EndPoint == line2.EndPoint;
        }

        // 判断是否形成 T 型连接
        private bool IsTConnection(Line line1, Line line2)
        {
            if (IsPointOnLine(line1.StartPoint, line2) && !IsPointEndPoint(line1.StartPoint, line2))
                return true;
            if (IsPointOnLine(line1.EndPoint, line2) && !IsPointEndPoint(line1.EndPoint, line2))
                return true;
            if (IsPointOnLine(line2.StartPoint, line1) && !IsPointEndPoint(line2.StartPoint, line1))
                return true;
            if (IsPointOnLine(line2.EndPoint, line1) && !IsPointEndPoint(line2.EndPoint, line1))
                return true;
            return false;
        }

        // 判断点是否在直线上
        private bool IsPointOnLine(Teigha.Geometry.Point3d point, Line line)
        {
            // 定义容差，用于处理浮点精度问题
            double tolerance = 1e-6;

            // 获取直线段的起点和终点
            Teigha.Geometry.Point3d start = line.StartPoint;
            Teigha.Geometry.Point3d end = line.EndPoint;

            // 计算直线向量和点到起点的向量
            Vector3d lineVector = end - start;
            Vector3d pointVector = point - start;

            // 手动计算直线向量的平方长度
            double lengthSq = lineVector.X * lineVector.X +
                             lineVector.Y * lineVector.Y +
                             lineVector.Z * lineVector.Z;

            // 计算点在直线上的投影比例
            double projection = pointVector.DotProduct(lineVector) / lengthSq;

            // 检查投影点是否在直线段范围内（0 到 1 之间）
            if (projection < 0 || projection > 1)
            {
                return false;
            }

            // 计算投影点坐标
            Teigha.Geometry.Point3d closestPoint = start + projection * lineVector;

            // 计算点到投影点的距离
            double distance = (point - closestPoint).Length;

            // 判断距离是否在容差范围内
            return distance < tolerance;
        }

        // 判断点是否是直线的端点
        private bool IsPointEndPoint(Teigha.Geometry.Point3d point, Line line)
        {
            return point == line.StartPoint || point == line.EndPoint;
        }


        public void ReadPointsFromFile(object sender, EventArgs e)
        {
            string filePath = "points.txt";
            List<Dictionary<Point3d, List<Point3d>>> groupConnections = new List<Dictionary<Point3d, List<Point3d>>>();
            List<List<Point3d>> groupTextPoints = new List<List<Point3d>>();
            List<List<string>> groupTextStrings = new List<List<string>>();
            Dictionary<Point3d, List<Point3d>> currentGroup = null;
            List<Point3d> currentGroupTextPoints = null;
            List<string> currentGroupTextStrings = null;
            bool isReadingPoints = false;

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("开始图纸分析"))
                    {
                        if (currentGroup != null)
                        {
                            groupConnections.Add(currentGroup);
                            groupTextPoints.Add(currentGroupTextPoints);
                            groupTextStrings.Add(currentGroupTextStrings);
                        }
                        currentGroup = new Dictionary<Point3d, List<Point3d>>(new Point3dComparer());
                        currentGroupTextPoints = new List<Point3d>();
                        currentGroupTextStrings = new List<string>();
                        isReadingPoints = true;
                    }
                    else if (isReadingPoints && line.StartsWith("StartPoint:"))
                    {
                        string[] parts = line.Split(new[] { "StartPoint:", "EndPoint:" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            Point3d startPoint = ParsePoint3d(parts[0]);
                            Point3d endPoint = ParsePoint3d(parts[1]);

                            if (!currentGroup.ContainsKey(startPoint))
                                currentGroup[startPoint] = new List<Point3d>();
                            if (!currentGroup.ContainsKey(endPoint))
                                currentGroup[endPoint] = new List<Point3d>();

                            currentGroup[startPoint].Add(endPoint);
                            currentGroup[endPoint].Add(startPoint);
                        }
                    }
                    else if (isReadingPoints)
                    {
                        string[] parts = line.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4 && parts[1].Contains("=") && parts[2].Contains("=") && parts[3].Contains("="))
                        {
                            string text = parts[0];
                            double x = double.Parse(parts[1].Split('=')[1]);
                            double y = double.Parse(parts[2].Split('=')[1]);
                            double z = 0;
                            Point3d textPoint = new Point3d(x, y, z);
                            currentGroupTextStrings.Add(text);
                            currentGroupTextPoints.Add(textPoint);
                        }
                    }
                }
                if (currentGroup != null)
                {
                    groupConnections.Add(currentGroup);
                    groupTextPoints.Add(currentGroupTextPoints);
                    groupTextStrings.Add(currentGroupTextStrings);
                }
            }

            using (Transaction trans = database.TransactionManager.StartTransaction())
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                BlockTable blockTable = trans.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord modelSpace = trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 遍历每个连接组织的点
                for (int groupIndex = 0; groupIndex < groupConnections.Count; groupIndex++)
                {
                    var connections = groupConnections[groupIndex];
                    var textPoints = groupTextPoints[groupIndex];
                    var textStrings = groupTextStrings[groupIndex];
                    Teigha.Colors.Color groupColor = colors[groupIndex % colors.Count]; // 依次使用不同颜色
                    HashSet<Point3d> visited = new HashSet<Point3d>(new Point3dComparer());

                    foreach (var kvp in connections)
                    {
                        if (visited.Contains(kvp.Key))
                            continue;

                        Point3d startPoint = FindStartPoint(kvp.Key, connections, visited);
                        if (startPoint == default(Point3d))
                            continue;

                        var polyline = new Polyline();
                        Point3d currentPoint = startPoint;

                        do
                        {
                            polyline.AddVertexAt(polyline.NumberOfVertices, new Point2d(currentPoint.X, currentPoint.Y), 0, 0, 0);
                            visited.Add(currentPoint);

                            Point3d nextPoint = connections[currentPoint].FirstOrDefault(p => !visited.Contains(p));
                            if (nextPoint == default(Point3d))
                                break;

                            currentPoint = nextPoint;
                        } while (true);

                        if (polyline.NumberOfVertices > 1)
                        {
                            for (int i = 0; i < polyline.NumberOfVertices; i++)
                            {
                                polyline.SetStartWidthAt(i, 4.0); // 设置起始宽度，2.0 是示例值，可以根据需要调整
                                polyline.SetEndWidthAt(i, 4.0);   // 设置结束宽度，2.0 是示例值，可以根据需要调整
                            }

                            modelSpace.AppendEntity(polyline);
                            trans.AddNewlyCreatedDBObject(polyline, true);
                            polyline.Color = groupColor; // 设置为组颜色
                        }
                    }

                    // 在指定点插入文本，确保文本颜色与组颜色一致
                    for (int i = 0; i < textPoints.Count; i++)
                    {
                        DBText dbText = new DBText
                        {
                            Position = textPoints[i],
                            TextString = textStrings[i], // 先设置当前文本
                            Height = 10,
                            Color = groupColor, // 设置文本颜色为组颜色
                        };

                        // 如果 i+1 在范围内，添加额外的文本
                        if (i + 1 < textStrings.Count)
                        {
                            dbText.TextString += "\n " + textStrings[i + 1];
                            i++; // 跳过下一个已处理的文本
                        }

                        modelSpace.AppendEntity(dbText);
                        trans.AddNewlyCreatedDBObject(dbText, true);
                        Console.WriteLine(dbText);
                    }

                }

                trans.Commit();
                pView.Invalidate();
            }
        }

        private Point3d ParsePoint3d(string part)
        {
            string[] coordinates = part.Trim().Split(new[] { 'X', '=', 'Y', 'Z', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (coordinates.Length == 3)
            {
                double x = double.Parse(coordinates[0], CultureInfo.InvariantCulture);
                double y = double.Parse(coordinates[1], CultureInfo.InvariantCulture);
                double z = double.Parse(coordinates[2], CultureInfo.InvariantCulture);
                return new Point3d(x, y, z);
            }
            throw new FormatException("无法解析点数据，格式应为 'X=1.0, Y=2.0, Z=3.0'");
        }

        private Point3d FindStartPoint(Point3d point, Dictionary<Point3d, List<Point3d>> connections, HashSet<Point3d> visited)
        {
            foreach (var kvp in connections)
            {
                if (visited.Contains(kvp.Key))
                    continue;

                // 查找只有一个连接点的端点，或循环直到找到起始点
                if (connections[kvp.Key].Count == 1)
                    return kvp.Key;
            }
            return default(Point3d);
        }

        class Point3dComparer : IEqualityComparer<Point3d>
        {
            public bool Equals(Point3d x, Point3d y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(Point3d obj)
            {
                return obj.GetHashCode();
            }
        }

        public void bf_toxianshuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TransactionManager tm = database.TransactionManager;
            using (Transaction tr = tm.StartTransaction())
            {
                using (BlockTableRecord modelSpace = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForRead))
                {
                    List<Arc> arcs = new List<Arc>();
                    List<DBText> txts = new List<DBText>();
                    List<MText> mtxts = new List<MText>();
                    List<Circle> cirs = new List<Circle>();
                    List<Circle> cirbx = new List<Circle>();
                    List<Line> lines = new List<Line>();

                    List<Point3d> bxs = new List<Point3d>();
                    List<Point3d> bxe = new List<Point3d>();
                    List<Point3d> jdq1 = new List<Point3d>();
                    List<Line> jdqr = new List<Line>();
                    List<Point3d> jdqkg = new List<Point3d>();
                    List<Point3d> jdqbx = new List<Point3d>();
                    List<DBText> jdqtxt = new List<DBText>();//保险丝信息

                    List<Teigha.DatabaseServices.Polyline> Polylines = new List<Teigha.DatabaseServices.Polyline>();
                    List<Poly> Polys = new List<Poly>();
                    List<my_line> my_lines = new List<my_line>();
                    foreach (ObjectId oid in modelSpace)
                    {
                        DBObject dbobj = tr.GetObject(oid, OpenMode.ForRead);
                        if (dbobj is Entity)
                        {
                            Entity entity = dbobj as Entity;
                            string enttype = entity.GetRXClass().Name;
                            //File.AppendAllText("E:\\ttc.txt", enttype + "\r\n"/*, Encoding.Default*/);
                            if (enttype == "AcDbMText")
                            {
                                MText acMText = entity as MText;
                                mtxts.Add(acMText);
                            }
                            if (enttype == "AcDbArc")
                            {
                                Arc acArc = entity as Arc;
                                arcs.Add(acArc);
                            }
                            if (enttype == "AcDbText")
                            {
                                DBText acText = entity as DBText;
                                txts.Add(acText);
                            }
                            if (enttype == "AcDbCircle")
                            {
                                Circle acCircle = entity as Circle;
                                cirs.Add(acCircle);
                            }
                            if (enttype == "AcDbLine")
                            {
                                Line acLine = entity as Line;
                                lines.Add(acLine);
                                my_line p1;
                                p1.line = acLine;
                                p1.visit = false;
                                my_lines.Add(p1);
                            }
                            if (enttype == "AcDbPolyline")
                            {
                                Teigha.DatabaseServices.Polyline acPolyline = entity as Teigha.DatabaseServices.Polyline;//筛选多段线段长度
                                if (acPolyline.GetLineSegmentAt(0).Length > 19)
                                {
                                    Poly pl;
                                    pl.line = acPolyline;
                                    pl.visit = false;
                                    Polys.Add(pl);
                                    //Polylines.Add(acPolyline);
                                }

                            }
                        }
                    }//读取图元

                    double DistancePointToLineSegment(Point3d point, Point3d lineStart, Point3d lineEnd)
                    {
                        double dx = lineEnd.X - lineStart.X;
                        double dy = lineEnd.Y - lineStart.Y;

                        if ((dx == 0) && (dy == 0))
                        {
                            // The line segment is actually a point.
                            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                        }

                        // Calculate the t that minimizes the distance.
                        double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

                        // See if this represents one of the segment's endpoints or a point in the middle.
                        if (t < 0)
                        {
                            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                        }
                        else if (t > 1)
                        {
                            return Math.Sqrt(Math.Pow(point.X - lineEnd.X, 2) + Math.Pow(point.Y - lineEnd.Y, 2));
                        }
                        else
                        {
                            Point3d closest = new Point3d(lineStart.X + t * dx, lineStart.Y + t * dy, 0);
                            return Math.Sqrt(Math.Pow(point.X - closest.X, 2) + Math.Pow(point.Y - closest.Y, 2));
                        }
                    }//计算点线距离函数
                    double form_leftx = Global.mlist.First();
                    double form_rightx = Global.mlist[2];
                    //double form_leftx = 5038;//请主动输入
                    //double form_rightx = 5886;
                    #region 得到表结构的端口四属性

                    List<DBText> cav_txt = new List<DBText>();
                    List<DBText> No_txt = new List<DBText>();
                    List<DBText> col_txt = new List<DBText>();
                    List<DBText> spec_txt = new List<DBText>();
                    List<DBText> rest_txt = new List<DBText>();
                    foreach (DBText i in txts)
                    {
                        if (form_leftx < i.Position.X && i.Position.X < form_rightx)
                        {
                            if (i.TextString.Length >= 3)
                            {
                                if (i.TextString.Substring(0, 3).ToLower() == "cav")
                                {
                                    cav_txt.Add(i);
                                    continue;
                                }
                                if (i.TextString.Substring(0, 2).ToLower() == "no")
                                {
                                    No_txt.Add(i);
                                    continue;
                                }
                                if (i.TextString.Substring(0, 3).ToLower() == "col")
                                {
                                    col_txt.Add(i);
                                    continue;
                                }
                                if (i.TextString.Substring(0, 3).ToLower() == "csa")
                                {
                                    spec_txt.Add(i);
                                    continue;
                                }

                            }
                            if (i.TextString.Length >= 4)
                            {
                                if (i.TextString.Substring(0, 4).ToLower() == "spec")
                                {
                                    spec_txt.Add(i);
                                    continue;
                                }
                            }
                            rest_txt.Add(i);
                        }

                    }
                    txts = rest_txt;
                    List<DBText> widtxt1 = new List<DBText>();
                    List<DBText> porttxt1 = new List<DBText>();
                    List<DBText> ltxt1 = new List<DBText>();
                    List<List<DBText>> xstxt1 = new List<List<DBText>>();

                    foreach (DBText i in No_txt)
                    {
                        Point3d cav_pos = i.Position;
                        List<DBText> lins = new List<DBText>();
                        for (int j = 0; j < txts.Count; j++)
                        {

                            if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f)
                            {
                                lins.Add(txts[j]);
                                cav_pos = txts[j].Position;
                                txts.RemoveAt(j);
                                j = 0;
                            }
                        }
                        xstxt1.Add(lins);
                    }
                    foreach (DBText i in cav_txt)
                    {
                        Point3d cav_pos = i.Position;
                        for (int j = 0; j < txts.Count; j++)
                        {

                            if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f)
                            {
                                porttxt1.Add(txts[j]);
                                cav_pos = txts[j].Position;
                                txts.RemoveAt(j);
                                j = 0;
                            }

                        }
                    }
                    foreach (DBText i in col_txt)
                    {
                        Point3d cav_pos = i.Position;
                        for (int j = 0; j < txts.Count; j++)
                        {

                            if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f)
                            {
                                ltxt1.Add(txts[j]);
                                cav_pos = txts[j].Position;
                                txts.RemoveAt(j);
                                j = 0;
                            }

                        }
                    }
                    foreach (DBText i in spec_txt)
                    {
                        Point3d cav_pos = i.Position;
                        for (int j = 0; j < txts.Count; j++)
                        {


                            if (Math.Abs(cav_pos.X - txts[j].Position.X) < 3f && Math.Abs(Math.Abs(cav_pos.Y - txts[j].Position.Y) - 4f) < 1f)
                            {
                                widtxt1.Add(txts[j]);
                                cav_pos = txts[j].Position;
                                txts.RemoveAt(j);
                                j = 0;
                            }

                        }
                    }
                    #endregion

                    #region 表结构的端口类
                    List<List<Port>> Ap2 = new List<List<Port>>();
                    List<Port> Ap3 = new List<Port>();//端口处理
                    foreach (List<DBText> p in xstxt1)
                    {
                        List<Port> Ap1 = new List<Port>();//端口处理
                        foreach (DBText i in p)
                        {
                            //if (i.TextString != "-")
                            {
                                DBText item1 = new DBText();
                                DBText item2 = new DBText();
                                DBText item3 = new DBText();
                                foreach (DBText j in widtxt1)
                                {
                                    if (j.Position.X > i.Position.X && Math.Abs(j.Position.X - i.Position.X) < 25f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                    {
                                        item3 = j;
                                        break;
                                    }
                                }
                                foreach (DBText j in ltxt1)
                                {
                                    if (j.Position.X > i.Position.X && Math.Abs(j.Position.X - i.Position.X) < 25f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                    {
                                        item2 = j;
                                        break;
                                    }
                                }
                                foreach (DBText j in porttxt1)
                                {
                                    if (Math.Abs(j.Position.X - i.Position.X) - 6 < 2f && Math.Abs(j.Position.Y - i.Position.Y) < 1f)
                                    {
                                        item1 = j;
                                        break;
                                    }
                                }
                                Port op = new Port(i, item1, item2, item3);
                                Ap1.Add(op);
                                Ap3.Add(op);
                            }
                        }
                        Ap2.Add(Ap1);

                    }
                    List<List<Port>> New_Ap = new List<List<Port>>();
                    List<Port> new_ap = new List<Port>();
                    //List<bool> ap_flag = new List<bool>(Ap2.Count());
                    List<bool> ap_flag = Enumerable.Repeat(false, Ap2.Count()).ToList();
                    //if (Ap2.Count != 0)
                    //{
                    //    for (int i = 0; i < Ap2.Count() - 1; i++)
                    //    {
                    //        List<Port> ap2 = Ap2[i];
                    //        DBText a = ap2[ap2.Count() - 1].port;
                    //        List<Port> ap3 = Ap2[i + 1];
                    //        DBText a3 = ap3[0].port;
                    //        string str1 = a.TextString;
                    //        string str2 = a3.TextString;
                    //        int num1, num2;
                    //        bool isNum1 = int.TryParse(str1, out num1);
                    //        bool isNum2 = int.TryParse(str2, out num2);
                    //        new_ap = ap2;
                    //        if (isNum1 && isNum2)
                    //        {
                    //            int diff = num2 - num1;
                    //            if (diff == 1)
                    //            {
                    //                if (ap_flag[i])
                    //                {
                    //                    new_ap = New_Ap[New_Ap.Count() - 1].Concat(ap3).ToList();
                    //                    New_Ap.RemoveAt(New_Ap.Count() - 1);
                    //                }
                    //                else
                    //                {
                    //                    new_ap = ap2.Concat(ap3).ToList();
                    //                }

                    //                ap_flag[i + 1] = true;
                    //            }

                    //        }
                    //        if (ap_flag[i] && new_ap == ap2)
                    //            continue;
                    //        New_Ap.Add(new_ap);
                    //    }
                    //}
                    //if (Ap2.Count != 0)
                    //{
                    //    if (!ap_flag[Ap2.Count() - 1])
                    //        New_Ap.Add(Ap2[Ap2.Count() - 1]);
                    //}

                    //Ap2 = New_Ap;
                    //List<List<Port>> New_Ap1 = new List<List<Port>>();//表格形式的接插件
                    //List<List<Port>> Mid_Ap1 = new List<List<Port>>();//表格形式的中间件
                    //foreach (List<Port> j in Ap2)
                    //{
                    //    List<Port> new_ap1 = new List<Port>();
                    //    foreach (Port i in j)
                    //    {
                    //        if (i.XS.TextString != "-")
                    //        {
                    //            new_ap1.Add(i);
                    //        }

                    //    }
                    //    if (j[0].port.TextString.All(char.IsDigit))
                    //        New_Ap1.Add(new_ap1);
                    //    if (j[0].port.TextString.All(char.IsLetter))
                    //        Mid_Ap1.Add(new_ap1);
                    //}
                    //Ap2 = New_Ap1;


                    //File.Delete("E:\\n2.txt");
                    //foreach (List<Port> j in Ap2)
                    //{
                    //    foreach (Port i in j)
                    //    {
                    //        //if (i.XS.TextString != "-")
                    //        {
                    //            File.AppendAllText("E:\\n2.txt", "线束名：" + i.XS.TextString + " " + i.XS.Position.X + " " + i.XS.Position.Y + " " + "\r\n");
                    //            File.AppendAllText("E:\\n2.txt", "端口：" + i.port.TextString + " " + i.port.Position.X + " " + i.port.Position.Y + " " + "\r\n");
                    //            File.AppendAllText("E:\\n2.txt", "颜色：" + i.color.TextString + " " + i.color.Position.X + " " + i.color.Position.Y + " " + "\r\n");
                    //            File.AppendAllText("E:\\n2.txt", "线宽：" + i.width.TextString + " " + i.width.Position.X + " " + i.width.Position.Y + " " + "\r\n\n");
                    //        }

                    //    }
                    //    File.AppendAllText("E:\\n2.txt", "\n\n");
                    //}
                    #endregion

                    //处理所有的文本，分成多类
                    bool isPureNum(string str)
                    {
                        bool isNumeric = int.TryParse(str, out _);
                        return isNumeric;
                    }
                    bool isfPureNum(string str)
                    {
                        bool isNumeric = float.TryParse(str, out _);
                        return isNumeric;
                    }
                    Regex regex = new Regex(@"\d+\.\d{1,2}");
                    Regex regey = new Regex("^[A-Z][a-zA-Z0-9]*$");
                    Regex regep = new Regex("^[a-zA-Z][a-zA-Z0-9][a-zA-Z]*$");
                    Regex regez = new Regex("^[N][1-3][0-9]*$");
                    Regex regeq = new Regex(@"^[\u4e00-\u9fa5]+(-[A-Z0-9]+)?$");//?表示可选第一种带-或第二种不带-
                    Regex regew = new Regex(@"^[a-zA-Z]+[\u4e00-\u9fa5]+$");
                    Regex regee = new Regex(@"^[a-zA-Z][0-9]+[\u4e00-\u9fa5]+$");
                    Regex reger = new Regex(@"^[\u4e00-\u9fa5]+[A-Z]+[\+]+$");
                    Regex reget = new Regex(@"^[\u4e00-\u9fa5]+[\（]+[\u4e00-\u9fa5]+[\）]+$");
                    Regex regeu = new Regex("^[0-9]+[a-zA-Z]+[0-9]+$");
                    Regex regei = new Regex("^[a-zA-Z]+[0-9]+[a-zA-Z]?$");
                    List<DBText> widtxt = new List<DBText>();
                    List<DBText> porttxt = new List<DBText>();
                    List<DBText> ltxt = new List<DBText>();
                    List<DBText> xstxt = new List<DBText>();
                    List<DBText> vetextxt = new List<DBText>();
                    double leftx = Global.mlist[0];//请主动输入
                    double rightx = Global.mlist[2];
                    double portmax = 160;//最大端口号

                    double downx = Global.mlist[3];
                    foreach (DBText i in txts)
                    {
                        if (i.Position.X > leftx && i.Position.X < rightx && i.Position.Y > downx && i.TextString.Length > 0)
                        {
                            if (regex.Matches(i.TextString) != null && regex.Matches(i.TextString).Count > 0 && isfPureNum(i.TextString))
                                widtxt.Add(i);
                            int num = int.TryParse(i.TextString, out num) ? num : 0;
                            if (isPureNum(i.TextString) && num < portmax && num > 0)
                                porttxt.Add(i);
                            if (i.TextString.Length == 1 && char.IsUpper(i.TextString[0]))
                            {
                                porttxt.Add(i);
                            }
                            if (i.TextString.Contains('/'))
                            {
                                if (Regex.IsMatch(i.TextString, @"^[a-zA-Z/]+$") && i.TextString.Length < 5)
                                    ltxt.Add(i);
                            }
                            else
                            {
                                if (Regex.IsMatch(i.TextString, @"^[a-zA-Z]+$") && i.TextString.Length <= 2)
                                    ltxt.Add(i);
                            }
                            if ((regey.IsMatch(i.TextString) && i.TextString.Length <= 6 && !regez.IsMatch(i.TextString))/*|| regep.IsMatch(i.TextString) && i.TextString.Length == 6*/)
                            {
                                bool hasLetter = false;
                                bool hasDigit = false;

                                foreach (char c in i.TextString)
                                {
                                    if (char.IsLetter(c))
                                    {
                                        hasLetter = true;
                                    }
                                    else if (char.IsDigit(c))
                                    {
                                        hasDigit = true;
                                    }
                                    if (i.TextString == "E115")
                                        break;
                                    if (hasLetter && hasDigit)
                                    {
                                        xstxt.Add(i);
                                        break;
                                    }
                                }
                            }
                            if (Math.Abs(i.Height - txt_heighe) < 0.1f && (regeq.IsMatch(i.TextString) || regew.IsMatch(i.TextString)) && i.TextString != "装配" && i.TextString != "橡胶件")
                            {
                                vetextxt.Add(i);
                            }
                            //if (Math.Abs(i.Height - txt_heighe) < 0.1f && i.Color.Red == 255 && i.Color.Green == 0 && i.Color.Blue == 0)//只为BD
                            //{
                            //    vetextxt.Add(i);
                            //}
                        }
                    }

                    AllPort Ap = new AllPort();//端口处理
                    foreach (DBText i in xstxt)
                    {
                        if (vetextxt.Contains(i))
                            continue;
                        DBText item1 = new DBText();
                        DBText item2 = new DBText();
                        DBText item3 = new DBText();
                        foreach (DBText j in widtxt)
                        {
                            if (Math.Abs(j.Position.X - i.Position.X) - 5.5 < 1f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                            {
                                item3 = j;
                                break;
                            }
                        }
                        foreach (DBText j in ltxt)
                        {
                            if (Math.Abs(j.Position.X - i.Position.X) - 1.8 < 1f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                            {
                                item2 = j;
                                break;
                            }
                        }
                        foreach (DBText j in porttxt)
                        {
                            if (Math.Abs(j.Position.X - i.Position.X) - 2.5 < 2f && Math.Abs(j.Position.Y - i.Position.Y) - 3.55 < 0.5f)
                            {
                                item1 = j;
                                break;
                            }
                        }
                        Port op = new Port(i, item1, item2, item3);
                        Ap.Add(op);
                    }


                    //File.Delete("E:\\ttc.txt");
                    //foreach (Port i in Ap.add)
                    //{
                    //    File.AppendAllText("E:\\ttc.txt", "线束名：" + i.XS.TextString + " " + i.XS.Position.X + " " + i.XS.Position.Y + " " + "\r\n");
                    //    File.AppendAllText("E:\\ttc.txt", "端口：" + i.port.TextString + " " + i.port.Position.X + " " + i.port.Position.Y + " " + "\r\n");
                    //    File.AppendAllText("E:\\ttc.txt", "颜色：" + i.color.TextString + " " + i.color.Position.X + " " + i.color.Position.Y + " " + "\r\n");
                    //    File.AppendAllText("E:\\ttc.txt", "线宽：" + i.width.TextString + " " + i.width.Position.X + " " + i.width.Position.Y + " " + "\r\n\n");
                    //}

                    bool IsClose(Point3d p1, Point3d p2, double distanceThreshold)
                    {
                        double distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
                        return distance <= distanceThreshold;
                    }

                    List<List<Port>> GroupPoints(List<Port> points, double distanceThreshold)
                    {
                        List<List<Port>> groups = new List<List<Port>>();
                        HashSet<Point3d> visited = new HashSet<Point3d>();

                        foreach (Port point in points)
                        {
                            if (!visited.Contains(point.XS.Position))
                            {
                                List<Port> group = new List<Port>();
                                group.Add(point);

                                visited.Add(point.XS.Position);

                                for (int i = 0; i < group.Count; i++)
                                {
                                    Point3d current = group[i].XS.Position;

                                    foreach (Port other in points)
                                    {
                                        if (!visited.Contains(other.XS.Position) && IsClose(current, other.XS.Position, distanceThreshold))
                                        {
                                            group.Add(other);
                                            visited.Add(other.XS.Position);
                                        }
                                    }
                                }

                                groups.Add(group);
                            }
                        }

                        return groups;
                    }
                    //是不是用oom
                    List<Port> ooo = new List<Port>();
                    List<DBText> ooom = new List<DBText>();
                    foreach (Port i in Ap.add)
                    {
                        if (i.color.Position.X != 0)
                            ooo.Add(i);
                        else
                            ooom.Add(i.XS);

                    }
                    //中间元件
                    bool poppb = false;
                    DBText popp = new DBText();
                    List<DBText> iio = new List<DBText>();
                    List<List<DBText>> iiom = new List<List<DBText>>();
                    foreach (var i in ooom)
                    {

                        if (!poppb)
                        {
                            iio = new List<DBText>();
                            iio.Add(i);
                            popp = i;
                            poppb = true;
                        }
                        else
                        {
                            if (Math.Abs(popp.Position.X - i.Position.X) < 1 && Math.Abs(Math.Abs(popp.Position.Y - i.Position.Y) - 4) < 1)
                            {
                                iio.Add(i);
                                popp = i;

                            }
                            else
                            {
                                iiom.Add(iio);
                                iio = new List<DBText>();
                                iio.Add(i);
                                popp = i;
                                poppb = true;
                            }

                        }


                    }

                    List<List<Port>> ppp = GroupPoints(ooo, 31);
                    //File.Delete("E:\\tt.txt");
                    //File.AppendAllText("E:\\tt.txt", ppp.Count + "\n");

                    #region 判断点在线的上方
                    bool IsPointAboveLineSegment(Point3d p1, Point3d p2, Point3d p)
                    {
                        if (p1.X == p2.X) // 直线竖直
                        {
                            return p.X < p1.X;
                        }
                        else // 直线非竖直
                        {
                            // 计算直线段的斜率和截距
                            double a = (p2.Y - p1.Y) / (p2.X - p1.X);
                            double b = p1.Y - a * p1.X;

                            // 判断点是否在直线的上方
                            bool isAboveLine = p.Y > a * p.X + b;

                            // 判断点是否在直线段的两个端点之间
                            bool isBetweenPoints = (p.X >= p1.X && p.X <= p2.X) || (p.X >= p2.X && p.X <= p1.X);

                            return isAboveLine && isBetweenPoints;
                        }
                    }
                    bool GetPointIsInLine(Point3d pf, Point3d p1, Point3d p2, double range)
                    {
                        //range 判断的的误差，不需要误差则赋值0
                        //点在线段首尾两端之外则return false
                        double cross = (p2.X - p1.X) * (pf.X - p1.X) + (p2.Y - p1.Y) * (pf.Y - p1.Y);
                        if (cross <= 0) return false;
                        double d2 = (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
                        if (cross >= d2) return false;
                        double r = cross / d2;
                        double px = p1.X + (p2.X - p1.X) * r;
                        double py = p1.Y + (p2.Y - p1.Y) * r;
                        //判断距离是否小于误差
                        return (pf.X - px) * (pf.X - px) + (py - pf.Y) * (py - pf.Y) <= range * range;
                    }
                    #endregion







                    //构建三元组
                    List<DBText> numtxt = new List<DBText>();
                    foreach (DBText i in txts)
                    {
                        if (isPureNum(i.TextString) && Math.Abs(i.Height - quanzhi_height) < 0.1f)
                            numtxt.Add(i);
                    }
                    List<tuple> tuple = new List<tuple>();
                    List<string> wei = new List<string>();
                    Queue<Point3d> findpoint = new Queue<Point3d>();

                    findpoint.Enqueue(startpoint);
                    while (findpoint.Count != 0)
                    {
                        Point3d midpoint = findpoint.Dequeue();
                        tuple midtuple;
                        midtuple.start = midpoint;
                        int j = 0;
                        for (int ij = 0; ij < Polys.Count; ij++)
                        {
                            Poly i = Polys[ij];
                            if (!i.visit)
                            {
                                if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(0).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(0).Y) < 0.1f)
                                {
                                    findpoint.Enqueue(i.line.GetPoint3dAt(1));
                                    midtuple.end = i.line.GetPoint3dAt(1);
                                    bool pp = false;
                                    foreach (DBText p in numtxt)
                                    {
                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis)
                                        {
                                            wei.Add(p.TextString);
                                            pp = true;
                                            break;
                                        }
                                    }
                                    if (!pp)
                                        wei.Add("0");
                                    tuple.Add(midtuple);
                                    i.visit = true;
                                    Polys[ij] = i;
                                    j++;
                                    if (j >= 6)
                                    {
                                        break;
                                    }
                                }
                                else if (Math.Abs(midpoint.X - i.line.GetPoint3dAt(1).X) < 0.1f && Math.Abs(midpoint.Y - i.line.GetPoint3dAt(1).Y) < 0.1f)
                                {
                                    findpoint.Enqueue(i.line.GetPoint3dAt(0));//该图中可以不用
                                    midtuple.end = i.line.GetPoint3dAt(0);
                                    bool pp = false;
                                    foreach (DBText p in numtxt)
                                    {
                                        if (DistancePointToLineSegment(p.Position, midtuple.start, midtuple.end) < wei_dis)
                                        {
                                            wei.Add(p.TextString);
                                            pp = true;
                                            break;
                                        }
                                    }
                                    if (!pp)
                                        wei.Add("0");
                                    tuple.Add(midtuple);
                                    i.visit = true;
                                    Polys[ij] = i;
                                    continue;

                                }
                            }
                        }
                    }//寻径电线

                    //File.Delete("E:\\tes.txt");
                    List<tuple3> tuple3 = new List<tuple3>();
                    for (int o = 0; o < tuple.Count; o++)
                    {
                        tuple3 tuple7;
                        tuple7.start = tuple[o].start;
                        tuple7.end = tuple[o].end;
                        int myInt = int.Parse(wei[o]);
                        tuple7.weight = myInt;
                        tuple3.Add(tuple7);
                        //File.AppendAllText("tes.txt", "zhixian1：" + tuple7.weight + " " + tuple7.start.X + " " + tuple7.start.Y + " " + tuple7.end.X + " " + tuple7.end.Y + " " + "\r\n");
                    }//中间三元组处理

                    WeightedUndirectedGraph gragh = new WeightedUndirectedGraph();
                    foreach (tuple3 i in tuple3)
                    {
                        int w = i.weight;
                        Point3d f = i.start;
                        Point3d t = i.end;
                        gragh.AddEdge(f, t, w);
                    }//初始化无向图

                    List<Point3d> vetex = gragh.RE();
                    Dictionary<Point3d, DBText> closestPoints = new Dictionary<Point3d, DBText>();
                    List<Point3d> repo = new List<Point3d>();
                    Dictionary<Point3d, List<double>> dicc = new Dictionary<Point3d, List<double>>();
                    Dictionary<Point3d, double> dic = new Dictionary<Point3d, double>();
                    //foreach (Point3d pt in vetex)
                    //{
                    //    double minDist = double.MaxValue - 1d;
                    //    DBText closestPt = new DBText();
                    //    double minDist1 = double.MaxValue;
                    //    DBText closestPt1 = new DBText();

                    //    foreach (DBText pttxt in vetextxt)
                    //    {
                    //        double dist = Math.Sqrt(Math.Pow(pttxt.Position.X - pt.X, 2) + Math.Pow(pttxt.Position.Y - pt.Y, 2));
                    //        if (dist < minDist)
                    //        {
                    //            minDist1 = minDist;
                    //            closestPt1 = closestPt;
                    //            minDist = dist;
                    //            closestPt = pttxt;
                    //        }
                    //        else if (dist < minDist1)
                    //        {
                    //            minDist1 = dist;
                    //            closestPt1 = pttxt;
                    //        }
                    //    }
                    //    if (closestPoints.ContainsValue(closestPt))
                    //    {
                    //        List<Point3d> keys = closestPoints.GetKeysByValue(closestPt);
                    //        closestPoints.Remove(keys[0]);
                    //        repo.Add(keys[0]);
                    //    }
                    //    closestPoints.Add(pt, closestPt);

                    //}//筛选末端节点

                    foreach (Point3d pt in vetex)
                    {
                        double minDist = double.MaxValue;

                        DBText closestPt = new DBText();
                        List<double> md = new List<double>();
                        Dictionary<DBText, double> dm = new Dictionary<DBText, double>();
                        foreach (DBText pttxt in vetextxt)
                        {
                            double dist = Math.Sqrt(Math.Pow(pttxt.Position.X - pt.X, 2) + Math.Pow(pttxt.Position.Y - pt.Y, 2));
                            //dm.Add(pttxt, dist);
                            //md.Add(dist);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestPt = pttxt;
                            }
                        }

                        dic.Add(pt, minDist);
                        if (closestPoints.ContainsValue(closestPt))
                        {
                            List<Point3d> keys = closestPoints.GetKeysByValue(closestPt);
                            if (dic[keys[0]] > minDist)
                            {
                                closestPoints.Remove(keys[0]);
                                repo.Add(keys[0]);
                                closestPoints.Add(pt, closestPt);

                            }
                            else
                            {
                                repo.Add(pt);

                            }

                        }
                        else
                        {
                            closestPoints.Add(pt, closestPt);

                        }





                        ////默认第二次正确
                        //if (closestPoints.ContainsValue(closestPt))
                        //{
                        //    List<Point3d> keys = closestPoints.GetKeysByValue(closestPt);
                        //    closestPoints.Remove(keys[0]);
                        //    repo.Add(keys[0]);
                        //}


                    }
                    File.Delete("ttc.txt");
                    foreach (var i in repo)
                    {
                        File.AppendAllText("ttc.txt", "未匹配的端点：" + " " + i.X + " " + i.Y + " " + "\r\n");
                        double minDist = double.MaxValue;
                        DBText closestPt = new DBText();
                        foreach (DBText pttxt in vetextxt)
                        {
                            if (!closestPoints.ContainsValue(pttxt))
                            {
                                double dist = Math.Sqrt(Math.Pow(pttxt.Position.X - i.X, 2) + Math.Pow(pttxt.Position.Y - i.Y, 2));
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    closestPt = pttxt;
                                }
                            }
                        }
                        closestPoints.Add(i, closestPt);
                    } //添加遗漏线束
                    List<DBText> weiszh = new List<DBText>();
                    foreach (DBText pttxt in vetextxt)
                    {
                        if (!closestPoints.ContainsValue(pttxt))//已经有的不考虑
                        {
                            File.AppendAllText("ttc.txt", "未匹配的接插件：" + pttxt.TextString + " " + pttxt.Position.X + " " + pttxt.Position.Y + " " + "\r\n");
                            weiszh.Add(pttxt);
                        }
                    }



                    Point3d bxsh = new Point3d();
                    Point3d oldbxsh = new Point3d();
                    DBText bxshtxt = new DBText();
                    foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                    {

                        if (i.Value.TextString == "主保险丝盒总成")
                        {
                            if (i.Value.Position.X > 2000)
                            {
                                oldbxsh = i.Key;
                                bxshtxt = i.Value;
                            }
                            else
                            {
                                bxsh = gragh.Neighbor(i.Key);
                            }
                        }

                    }
                    closestPoints.Remove(oldbxsh);
                    closestPoints.Add(bxsh, bxshtxt);


                    Dictionary<List<Port>, DBText> jiechajian = new Dictionary<List<Port>, DBText>();
                    foreach (KeyValuePair<Point3d, DBText> i in closestPoints)
                    {
                        Point3d p = i.Value.Position;
                        double minD = double.MaxValue;
                        List<Port> po = new List<Port>();
                        foreach (List<Port> j in ppp)
                        {
                            double minDist = double.MaxValue;
                            foreach (Port u in j)
                            {
                                double dist = Math.Sqrt(Math.Pow(u.XS.Position.X - p.X, 2) + Math.Pow(u.XS.Position.Y - p.Y, 2));
                                if (dist < minDist)
                                    minDist = dist;
                            }
                            if (minDist < minD)
                            {
                                minD = minDist;
                                po = j;
                            }
                        }
                        if (jiechajian.ContainsKey(po))
                        {

                        }
                        //File.AppendAllText("E:\\tt.txt", "因重复没有接插件的端口：" + po.Count + " " + po[0].XS.TextString /*+" " + po[1].XS.TextString */+ " " + i.Value.TextString + " " + i.Value.Position.X + "  " + i.Value.Position.Y + "\r\n");
                        else
                            jiechajian.Add(po, i.Value);
                    }
                    //File.AppendAllText("E:\\tt.txt", "文字：" + jiechajian.Count() + "\r\n");
                    var jiechajiankeys = jiechajian.Keys.ToList();
                    List<Port> popo = new List<Port>();
                    DBText poi = new DBText();

                    foreach (List<Port> j in ppp)
                    {

                        if (j[0].XS.TextString == "P03")
                        {
                            popo = j;
                        }

                    }
                    foreach (var key in jiechajiankeys)
                    {
                        if (jiechajian[key].TextString == "主配电盒总成")
                        {
                            poi = jiechajian[key];
                            popo.Add(key[0]);
                            jiechajian.Remove(key);
                            jiechajian.Add(popo, poi);
                            ppp.Remove(key);
                        }
                    }
                    List<Port> bxsport = new List<Port>();
                    DBText bxsop = new DBText();
                    foreach (KeyValuePair<List<Port>, DBText> i in jiechajian)//输出检测
                    {
                        if (i.Value.TextString == "主保险丝盒总成" && i.Value.Position.X > 2000)
                        {
                            bxsop = i.Value;
                        }
                    }
                    jiechajian.Remove(bxsport);
                    foreach (List<Port> j in ppp)
                    {
                        if (!jiechajian.ContainsKey(j))
                        {
                            bxsport.AddRange(j);
                        }

                    }
                    if (bxsop.Position.X != 0)
                        jiechajian.Add(bxsport, bxsop);

                    List<Port_jc> pjc = new List<Port_jc>();
                    foreach (KeyValuePair<List<Port>, DBText> i in jiechajian)//输出检测
                    {
                        //File.AppendAllText("E:\\tt.txt", "文字：" + i.Value.TextString + " " + i.Value.Position.X + " " + i.Value.Position.Y + " " + "\r\n");
                        KeyValuePair<Point3d, DBText> iip = new KeyValuePair<Point3d, DBText>();
                        foreach (KeyValuePair<Point3d, DBText> iii in closestPoints)
                        {
                            if (iii.Value == i.Value)
                            {
                                //File.AppendAllText("E:\\tt.txt", "文字：" + iii.Key.X + " " + iii.Key.Y + " " + "\r\n");
                                iip = iii;
                            }

                        }
                        foreach (Port j in i.Key)
                        {
                            //File.AppendAllText("E:\\tt.txt", "线束名：" + j.XS.TextString + " " + j.XS.Position.X + " " + j.XS.Position.Y + " " + "\r\n");
                            Port_jc opo = new Port_jc(iip.Value, j, iip.Key);
                            pjc.Add(opo);
                        }
                        //File.AppendAllText("E:\\tt.txt", "\n\n");
                    }




                    //File.Delete("E:\\test.txt");
                    //foreach (var pop in pjc)
                    //{
                    //    File.AppendAllText("E:\\test.txt", "线束标号：" + pop.pot.XS.TextString + "\r\n");
                    //    File.AppendAllText("E:\\test.txt", "接插件名称及接口：" + pop.jc.TextString + " " + pop.pot.port.TextString + "\r\n");
                    //    File.AppendAllText("E:\\test.txt", "线色和线径：" + pop.pot.color.TextString + " " + pop.pot.width.TextString + "\r\n");
                    //    File.AppendAllText("E:\\test.txt", "寻径点：" + pop.vet.X + " " + pop.vet.Y + "\r\n");
                    //    File.AppendAllText("E:\\test.txt", "\n\n");
                    //}

                    File.Delete("text.txt");
                    //List<Portsc> sc = new List<Portsc>();
                    var oogroup = ooo.GroupBy(p => p.XS.TextString);
                    foreach (var group in oogroup)
                    {
                        string thistext = group.Key;//线束名
                        //if (group.Count() != 2)
                        //{
                        //    foreach (var pot in group)
                        //    {
                        //        File.AppendAllText("E:\\test.txt", "线束名：" + pot.XS.TextString + " " + pot.XS.Position.X + " " + pot.XS.Position.Y + " " + "\r\n");
                        //    }
                        //    File.AppendAllText("E:\\test.txt", "\n\n");
                        //}
                        if (group.Count() == 2)
                        {
                            List<Point3d> epoint = new List<Point3d>();
                            File.AppendAllText("text.txt", "线束标号：" + thistext + " " + "\r\n");
                            File.AppendAllText("text.txt", "线色和线径：" + group.First().color.TextString + " " + group.First().width.TextString + "\r\n");
                            //File.AppendAllText("E:\\text.txt", "\n");
                            int ijishi = 0;
                            DBText linshijiecha = new DBText();
                            //DBText linshijiecha1 = new DBText();
                            foreach (var pot in group)
                            {

                                foreach (var pop in pjc)//输出检测
                                {
                                    if (pop.pot.XS == pot.XS)
                                    {
                                        if (ijishi == 0)
                                        {
                                            File.AppendAllText("text.txt", "起点接口：" + pop.jc.TextString + "/PIN" + pop.pot.port.TextString + "\r\n");
                                            //linshijiecha = pop.jc;
                                        }

                                        else
                                        {
                                            //linshijiecha1 = pop.jc;
                                            File.AppendAllText("text.txt", "终点接口：" + pop.jc.TextString + "/PIN" + pop.pot.port.TextString + "\r\n");
                                        }
                                        //File.AppendAllText("E:\\text.txt", "线色和线径：" + pop.pot.color.TextString + " " + pop.pot.width.TextString + "\r\n");
                                        //File.AppendAllText("E:\\text.txt", "寻径点：" + pop.vet.X + " " + pop.vet.Y + "\r\n");
                                        linshijiecha = pop.jc;
                                        epoint.Add(pop.vet);

                                    }
                                }
                                ijishi++;
                                //File.AppendAllText("E:\\text.txt", "\n");
                            }
                            int le = 0;
                            if (epoint.Count == 2)
                                le = gragh.DFS(epoint[0], epoint[1]);
                            //Portsc ls = new Portsc(group.First(), group.Last(), le, linshijiecha,linshijiecha1);
                            //sc.Add(ls);
                            File.AppendAllText("text.txt", "线束长度：" + le + "\r\n");
                            //File.AppendAllText("E:\\text.txt", "\n\n");
                        }

                        //if (group.Count() > 2)
                        //    Console.WriteLine("这些线束的端口超过两个");

                        //if (group.Count() == 1)
                        //    Console.WriteLine("这些线束的一端端口在中间件上，需要您自行决定走向");
                    }
                    //Wbjc = sc;

                }
            }

        }

        //public class TextWithCoordinates
        //{
        //    // 字符串属性，存储文本
        //    public string Text { get; set; }

        //    // Point3d 属性，存储坐标信息
        //    public Point3d Coordinates { get; set; }

        //    // 默认构造函数
        //    public TextWithCoordinates()
        //    {
        //        Text = string.Empty;  // 默认值
        //        Coordinates = new Point3d(0, 0, 0);  // 默认坐标
        //    }

        //    // 构造函数，用于初始化类实例
        //    public TextWithCoordinates(string text, Point3d coordinates)
        //    {
        //        Text = text;
        //        Coordinates = coordinates;
        //    }

        //    // 重写 ToString 方法，方便输出类的内容
        //    public override string ToString()
        //    {
        //        return $"Text: {Text}, Coordinates: ({Coordinates.X}, {Coordinates.Y}, {Coordinates.Z})";
        //    }
        //}

        public void fenpei_toTxtToolStripMenuItem_Click(object sender, EventArgs e)//重点修改
        {

            TransactionManager tm = database.TransactionManager;
            using (Transaction tr = tm.StartTransaction())
            {
                using (BlockTableRecord modelSpace = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForRead))
                {
                    double x1 = 0f, y1 = 0f, z1 = 0f, x2 = 0f, y2 = 0f, z2 = 0f;
                    List<Arc> arcs = new List<Arc>();
                    //List<DBText> txts = new List<DBText>();
                    List<MText> mtxts = new List<MText>();
                    List<Circle> cirs = new List<Circle>();
                    List<Circle> cirbx = new List<Circle>();
                    List<Line> lines = new List<Line>();
                    //List<DBText> bxtxt = new List<DBText>();//保险丝信息
                    List<TextWithCoordinates> listbxs = new List<TextWithCoordinates>();//保险丝
                    List<Point3d> bxs = new List<Point3d>();
                    List<Point3d> bxe = new List<Point3d>();
                    List<Point3d> jdq1 = new List<Point3d>();
                    List<Line> jdqr = new List<Line>();
                    List<TextWithCoordinates> listjdq = new List<TextWithCoordinates>();//继电器
                    List<Point3d> jdqkg = new List<Point3d>();
                    List<Point3d> jdqbx = new List<Point3d>();
                    //List<DBText> jdqtxt = new List<DBText>();//保险丝信息
                    List<Teigha.DatabaseServices.Polyline> Polylines = new List<Teigha.DatabaseServices.Polyline>();
                    //处理图纸实体
                    //leftx = Global.mlist.First();
                    //rightx = Global.mlist[2];
                    // 创建一个 List 来存储 TextWithCoordinates 对象
                    List<TextWithCoordinates> listOfTextWithCoords = new List<TextWithCoordinates>();
                    foreach (ObjectId oid in modelSpace)
                    {
                        DBObject dbobj = tr.GetObject(oid, OpenMode.ForRead);
                        if (dbobj is Entity)
                        {
                            Entity entity = dbobj as Entity;
                            string enttype = entity.GetRXClass().Name;
                            //File.AppendAllText("ttc.txt", enttype + "\r\n"/*, Encoding.Default*/);
                            //多行文本
                            if (enttype == "AcDbMText")
                            {
                                MText acMText = entity as MText;
                                mtxts.Add(acMText);

                                // 获取文本内容和坐标
                                string textContent = acMText.Text;
                                Point3d textPosition = acMText.Location;
                                // 创建并存储到 TextWithCoordinates 类
                                TextWithCoordinates mTextWithCoords = new TextWithCoordinates(textContent, textPosition);
                                // 将其添加到 listOfTextWithCoords 列表中
                                listOfTextWithCoords.Add(mTextWithCoords);
                            }
                            //弧形
                            if (enttype == "AcDbArc")
                            {
                                Arc acArc = entity as Arc;
                                arcs.Add(acArc);
                            }
                            //单行文本
                            if (enttype == "AcDbText")
                            {
                                DBText acText = entity as DBText;
                                //txts.Add(acText);

                                // 获取文本内容和坐标
                                string textContent = acText.TextString;
                                Point3d textPosition = acText.Position;
                                // 创建并存储到 TextWithCoordinates 类
                                TextWithCoordinates textWithCoords = new TextWithCoordinates(textContent, textPosition);
                                // 将其添加到 listOfTextWithCoords 列表中
                                listOfTextWithCoords.Add(textWithCoords);
                            }
                            //圆
                            if (enttype == "AcDbCircle")
                            {
                                Circle acCircle = entity as Circle;
                                cirs.Add(acCircle);
                            }
                            //Line
                            if (enttype == "AcDbLine")
                            {
                                Line acLine = entity as Line;
                                lines.Add(acLine);
                            }
                            //Polyline
                            if (enttype == "AcDbPolyline")
                            {
                                Teigha.DatabaseServices.Polyline acPolyline = entity as Teigha.DatabaseServices.Polyline;
                                Polylines.Add(acPolyline);
                            }
                        }
                    }                    

                    for (int k = listOfTextWithCoords.Count - 1; k >= 0; k--)
                    {
                        // 获取 x 和 y 坐标的最大值和最小值
                        double maxX = Math.Max(Global.x1, Global.x2);
                        double minX = Math.Min(Global.x1, Global.x2);
                        double maxY = Math.Max(Global.y1, Global.y2);
                        double minY = Math.Min(Global.y1, Global.y2);

                        // 判断条件：X和Y都在指定范围内
                        if (listOfTextWithCoords[k].Coordinates.X > maxX || listOfTextWithCoords[k].Coordinates.X < minX ||
                            listOfTextWithCoords[k].Coordinates.Y > maxY || listOfTextWithCoords[k].Coordinates.Y < minY)
                        {
                            listOfTextWithCoords.RemoveAt(k); // 移除
                        }
                    }

                    //for (int k = lines.Count - 1; k >= 0; k--)
                    //{
                    //    // 获取 x 和 y 坐标的最大值和最小值
                    //    double maxX = Math.Max(Global.x1, Global.x2);
                    //    double minX = Math.Min(Global.x1, Global.x2);
                    //    double maxY = Math.Max(Global.y1, Global.y2);
                    //    double minY = Math.Min(Global.y1, Global.y2);

                    //    // 判断条件：X和Y都在指定范围内
                    //    if (lines[k].StartPoint.X > maxX || lines[k].StartPoint.X < minX ||
                    //        lines[k].StartPoint.Y > maxY || lines[k].StartPoint.Y < minY)
                    //    {
                    //        lines.RemoveAt(k); // 移除
                    //    }
                    //}
                    //Console.WriteLine("剩余线段数：" + lines.Count);


                    //验证中间类的存储是否正常
                    File.Delete("outputzjl.txt");
                    foreach (var item in listOfTextWithCoords)
                    {
                        File.AppendAllText("outputzjl.txt", $"Text: {item.Text}, Coordinates: ({item.Coordinates.X}, {item.Coordinates.Y}, {item.Coordinates.Z})\n");
                    }

                    //通过几何方法判断一个点是否足够靠近一个直线段
                    bool GetPointIsInLine(Point3d pf, Point3d p1, Point3d p2, double range)
                     {
                        //range 判断的的误差，不需要误差则赋值0
                        //点在线段首尾两端之外则return false
                        double cross = (p2.X - p1.X) * (pf.X - p1.X) + (p2.Y - p1.Y) * (pf.Y - p1.Y);
                        if (cross <= 0) return false;
                        double d2 = (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
                        if (cross >= d2) return false;
                        double r = cross / d2;
                        double px = p1.X + (p2.X - p1.X) * r;
                        double py = p1.Y + (p2.Y - p1.Y) * r;
                        //判断距离是否小于误差
                        return (pf.X - px) * (pf.X - px) + (py - pf.Y) * (py - pf.Y) <= range * range;
                    }
                    //for (int i = 0; i < lines.Count; i++)
                    //{
                    //    for (int ij = 0; ij < cirs.Count; ij++)
                    //    {
                    //        if (GetPointIsInLine(cirs[ij].Center, lines[i].StartPoint, lines[i].EndPoint, 0.1) && Math.Abs(cirs[ij].Radius - 0.48) < 0.01)
                    //        {
                    //            lines.RemoveAt(i);
                    //            Line mline = new Line(lines[i].StartPoint, cirs[ij].Center);
                    //            Line nline = new Line(lines[i].StartPoint, cirs[ij].Center);
                    //            lines.Insert(i, mline);
                    //            lines.Insert(i, nline);
                    //        }
                    //    }
                    //}
                    //根据Y值进行升序排序
                    lines.Sort((t1, t2) => t1.StartPoint.Y >= t2.StartPoint.Y ? 1 : -1);



                    //除掉箭头
                    List<Line> lineq = new List<Line>();
                    for (int i = 0; i < lines.Count; i++)
                    {

                        if (Math.Abs(lines[i].Length - 0.5) < 0.01f || Math.Abs(lines[i].Length - 1.21) < 0.01f)
                            continue;
                        lineq.Add(lines[i]);
                    }
                    lines = lineq;

                    File.Delete("jtol.txt");
                    for (int i = 0; i < lines.Count; i++)
                    {
                        // 拼接每条线段的起点、终点和长度
                        string lineInfo = $"Start: ({lines[i].StartPoint.X}, {lines[i].StartPoint.Y}), " +
                                          $"End: ({lines[i].EndPoint.X}, {lines[i].EndPoint.Y}), " +
                                          $"Length: {lines[i].Length}\r\n";

                        // 将拼接后的信息追加到文件中
                        File.AppendAllText("jtol.txt", lineInfo);
                    }

                    //越界错误
                    //删除以下条件的线
                    //线对象的起点X坐标是否接近20987.64（允许误差为0.1）。
                    //线对象的起点Y坐标是否接近-661.85（因为Y坐标是负数，所以这里是加661.85）
                    //线对象的终点X坐标是否接近20976.77。
                    //线对象的终点Y坐标是否接近-661.85。
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (Math.Abs(lines[i].StartPoint.X - 20987.64) < 0.1 && Math.Abs(lines[i].StartPoint.Y + 661.85) < 0.1 && Math.Abs(lines[i].EndPoint.X - 20976.77) < 0.1 && Math.Abs(lines[i].EndPoint.Y + 661.85) < 0.1)
                            lines.RemoveAt(i);
                        //同时删除那些与已删除线对象起点和终点相匹配的线对象，无论是正向匹配还是反向匹配
                        for (int pp = i + 1; pp < lines.Count; pp++)
                        {
                            if (lines[i].StartPoint == lines[pp].StartPoint && lines[i].EndPoint == lines[pp].EndPoint || lines[i].StartPoint == lines[pp].EndPoint && lines[i].EndPoint == lines[pp].StartPoint)
                                lines.RemoveAt(pp);

                        }
                    }
                    //多段线排序
                    mtxts.Sort((t1, t2) => t1.Location.X >= t2.Location.X ? 1 : -1);
                    Polylines.Sort((t1, t2) => t1.GetPoint3dAt(0).X >= t2.GetPoint3dAt(0).X ? 1 : -1);
                    //txts.Sort((t1, t2) => t1.Position.X >= t2.Position.X ? 1 : -1);
                    cirs.Sort((t1, t2) => t1.Center.Y >= t2.Center.Y ? 1 : -1);
                    //cirs.Sort((x, y) => -x.Center.Y.CompareTo(y.Center.Y));

                    //筛选半径接近0.43的圆
                    for (int i = 0; i < cirs.Count; i++)
                    {
                        if (Math.Abs(cirs[i].Radius - Global.circleRadius) < 0.01f)
                        {
                            cirbx.Add(cirs[i]);
                        }
                    }

                    //    //保险丝两端直线筛选
                    //lines.Sort((t1, t2) => t1.StartPoint.Y >= t2.StartPoint.Y ? 1 : -1);

                    //File.Delete("tt.txt");
                    ////保险丝位置及名称信息
                    //保险丝位置的圆为什么这么小
                    int j = 1;
                    for (int i = 0; i < cirbx.Count - 1; i++)
                    {
                        //判断两个圆心是否接近
                        if (Math.Abs(cirbx[i].Center.Y - cirbx[i + 1].Center.Y) < 0.01f && Math.Abs(Math.Abs(cirbx[i].Center.X - cirbx[i + 1].Center.X) - Global.circleCenterDistance) < 0.01f)
                        {
                            //File.AppendAllText("tt.txt", cirbx[i].Center.X + " " + cirbx[i].Center.Y + " " + cirbx[i].Radius + "\r\n");
                            //File.AppendAllText("tt.txt", cirbx[i + 1].Center.X + " " + cirbx[i + 1].Center.Y + " " + cirbx[i + 1].Radius + "\r\n" + "Circle " + j + "\r\n");
                            //记录相邻的圆的个数
                            //根据圆形对象的中心X坐标的比较结果，将较小的中心坐标添加到bxs列表中，将较大的中心坐标添加到bxe列表中。
                            j++;
                            if (cirbx[i].Center.X < cirbx[i + 1].Center.X)
                            {
                                bxs.Add(cirbx[i].Center);
                                bxe.Add(cirbx[i + 1].Center);
                            }
                            else
                            {
                                bxs.Add(cirbx[i + 1].Center);
                                bxe.Add(cirbx[i].Center);
                            }
                            //判断文本对象的位置是否接近当前相邻圆形对象的中心，并且文本对象的Y坐标大于圆形对象的中心Y坐标。
                            //for (int k = 0; k < txts.Count; k++)
                            //{
                            //    //如果满足条件，则将该文本对象添加到bxtxt列表中，并从txts列表中删除它。
                            //    if ((Math.Abs(cirbx[i].Center.X - txts[k].Position.X) < 4f && Math.Abs(cirbx[i].Center.Y - txts[k].Position.Y) < 2f && txts[k].Position.Y - cirbx[i].Center.Y > 0) || (Math.Abs(cirbx[i + 1].Center.X - txts[k].Position.X) < 4f && Math.Abs(cirbx[i + 1].Center.Y - txts[k].Position.Y) < 2f && txts[k].Position.Y - cirbx[i + 1].Center.Y > 0))
                            //    {
                            //        //File.AppendAllText("tt.txt", txts[k].TextString + "\r\n");
                            //        bxtxt.Add(txts[k]);
                            //        txts.RemoveAt(k);//移除txt里保险丝信息 //这块关于bxtxt仍不能注释掉，貌似是影响到了txts，会导致输出excel内容变少
                            //    }
                            //}

                            for (int k = 0; k < listOfTextWithCoords.Count; k++)
                            {
                                if ((Math.Abs(cirbx[i].Center.X - listOfTextWithCoords[k].Coordinates.X) < 4f && Math.Abs(cirbx[i].Center.Y - listOfTextWithCoords[k].Coordinates.Y) < 2f && listOfTextWithCoords[k].Coordinates.Y - cirbx[i].Center.Y > 0) || (Math.Abs(cirbx[i + 1].Center.X - listOfTextWithCoords[k].Coordinates.X) < 4f && Math.Abs(cirbx[i + 1].Center.Y - listOfTextWithCoords[k].Coordinates.Y) < 2f && listOfTextWithCoords[k].Coordinates.Y - cirbx[i + 1].Center.Y > 0))
                                {
                                    //File.AppendAllText("tt.txt", txts[k].TextString + "\r\n");
                                    listbxs.Add(listOfTextWithCoords[k]);
                                    listOfTextWithCoords.RemoveAt(k);//移除保险丝信息

                                }
                            }

                        }
                    }
                    //验证保险丝内容存储正常
                    //File.Delete("listbxs.txt");
                    //foreach (var item in listbxs)
                    //{
                    //    File.AppendAllText("listbxs.txt", $"Text: {item.Text}, Coordinates: ({item.Coordinates.X}, {item.Coordinates.Y}, {item.Coordinates.Z})\n");
                    //}

                    //File.Delete("bxtxt.txt");
                    //for (int i = 0; i < bxtxt.Count; i++)
                    //{
                    //    File.AppendAllText("bxtxt.txt", bxtxt[i].TextString + "\r\n");
                    //}
                    //jdpr做line的中间量
                    /*
                        首先，将所有线对象从lines复制到jdqr。
                        然后，遍历jdqr，找到长度接近6.34的线对象。
                        对于每个长度接近6.34的线对象，再次遍历jdqr，查找长度接近5.03且起点与当前线对象终点相匹配的线对象。
                        将符合条件的线对象的起点坐标添加到jdq1，并将相关的文本对象添加到jdqtxt。
                        最后，从jdqr中删除这些找到的线对象
                     */
                    for (int i = 0; i < lines.Count; i++)
                    {
                        jdqr.Add(lines[i]);
                    }
                    for (int i = 0; i < jdqr.Count; i++)
                    {
                        if (Math.Abs(jdqr[i].Length - Global.jdqrectangleLength) < 0.01f)
                        {
                            for (int pp = 0; pp < jdqr.Count; pp++)
                            {
                                if (Math.Abs(jdqr[pp].Length - Global.jdqrectangleWidth) < 0.01f && jdqr[i].EndPoint == jdqr[pp].StartPoint && jdqr[pp].EndPoint.X > jdqr[pp].StartPoint.X)
                                {
                                    jdq1.Add(jdqr[pp].StartPoint);
                                    //for (int k = 0; k < txts.Count; k++)
                                    //{
                                    //    if (Math.Abs(jdqr[pp].StartPoint.X - txts[k].Position.X) < 5.71f && Math.Abs(jdqr[pp].StartPoint.Y - txts[k].Position.Y) < 8.27f && (jdqr[pp].StartPoint.Y > txts[k].Position.Y))
                                    //    {
                                    //        jdqtxt.Add(txts[k]);
                                    //        txts.RemoveAt(k);//移除txt里继电器信息
                                    //    }
                                    //}

                                    for (int k = 0; k < listOfTextWithCoords.Count; k++)
                                    {
                                        if (Math.Abs(jdqr[pp].StartPoint.X - listOfTextWithCoords[k].Coordinates.X) < 5.71f && Math.Abs(jdqr[pp].StartPoint.Y - listOfTextWithCoords[k].Coordinates.Y) < 8.27f && (jdqr[pp].StartPoint.Y > listOfTextWithCoords[k].Coordinates.Y) && listOfTextWithCoords[k].Text.Contains("继电器"))
                                        {
                                            listjdq.Add(listOfTextWithCoords[k]);
                                            listOfTextWithCoords.RemoveAt(k);
                                        }
                                    }

                                    jdqr.RemoveAt(pp);
                                    jdqr.RemoveAt(i);
                                }
                            }
                        }
                    }

                    //File.Delete("jdqtxt.txt");
                    //for (int i = 0; i < jdqtxt.Count; i++)
                    //{
                    //    File.AppendAllText("jdqtxt.txt", jdqtxt[i].TextString + "\r\n");
                    //}
                    //File.Delete("listjdq.txt");
                    //foreach (var item in listjdq)
                    //{
                    //    File.AppendAllText("listjdq.txt", $"Text: {item.Text}, Coordinates: ({item.Coordinates.X}, {item.Coordinates.Y}, {item.Coordinates.Z})\n");
                    //}

                    /*
                        从jdq1列表中读取每个点的坐标。
                        根据特定的偏移量（-0.91, -1.84, +5.93, -2.57, -0.84, -4.75）计算出新的点坐标。
                        将这些新点坐标分别添加到jdqkg和jdqbx列表中。
                     */

                    Point3d jdqp = new Point3d();
                    for (int pp = 0; pp < jdq1.Count; pp++)
                    {
                        jdqp = new Point3d(jdq1[pp].X - 0.91, jdq1[pp].Y - 1.84, jdq1[pp].Z);
                        jdqkg.Add(jdqp);
                        jdqp = new Point3d(jdq1[pp].X + 5.93, jdq1[pp].Y - 2.57, jdq1[pp].Z);
                        jdqkg.Add(jdqp);
                        jdqp = new Point3d(jdq1[pp].X + 5.93, jdq1[pp].Y - 0.84, jdq1[pp].Z);
                        jdqkg.Add(jdqp);
                        jdqp = new Point3d(jdq1[pp].X - 0.91, jdq1[pp].Y - 4.75, jdq1[pp].Z);
                        jdqbx.Add(jdqp);
                        jdqp = new Point3d(jdq1[pp].X + 5.93, jdq1[pp].Y - 4.75, jdq1[pp].Z);
                        jdqbx.Add(jdqp);
                    }


                    //bool PointOnLine(Point3d Pi, Point3d Pj, Point3d Q)
                    //{
                    //    if ((Q.X - Pi.X) * (Pj.Y - Pi.Y) == (Pj.X - Pi.X) * (Q.Y - Pi.Y)  //叉乘 
                    //                                                                      //保证Q点坐标在pi,pj之间 
                    //       && Math.Min(Pi.X, Pj.X) <= Q.X && Q.X <= Math.Max(Pi.X, Pj.X)
                    //       && Math.Min(Pi.Y, Pj.Y) <= Q.Y && Q.Y <= Math.Max(Pi.Y, Pj.Y))
                    //        return true;
                    //    else
                    //        return false;
                    //}





                    //蓄电池起点确定
                    /*
                    从Polylines列表中找出具有4个顶点且特定顶点Y坐标差异较大的多段线对象。
                    对于这些多段线对象，找出其第0个顶点坐标与另一个多段线对象的第0个顶点坐标在X轴上接近但在Y轴上差异极小的多段线对象。
                    计算这些多段线对象的第0个和第1个顶点坐标的中点坐标。
                     */
                    for (int i = 0; i < Polylines.Count - 1; i++)
                    {
                        for (int k = i + 1; k < Polylines.Count; k++)
                        {
                            //检查外层循环中的多段线对象Polylines[i]是否具有4个顶点。检查该多段线对象的第0个顶点和第2个顶点的Y坐标之差的绝对值是否大于1
                            if (Polylines[i].NumberOfVertices == 4 && Math.Abs(Polylines[i].GetPoint3dAt(0).Y - Polylines[i].GetPoint3dAt(2).Y) > 1f)
                            {
                                //检查外层循环中的多段线对象的第0个顶点坐标与内层循环中的多段线对象的第0个顶点坐标的X坐标差异是否小于12，且Y坐标差异是否小于0.01。
                                if (Math.Abs(Polylines[i].GetPoint3dAt(0).X - Polylines[k].GetPoint3dAt(0).X) < 12f && Math.Abs(Polylines[i].GetPoint3dAt(0).Y - Polylines[k].GetPoint3dAt(0).Y) < 0.01f)
                                {
                                    //计算两个点坐标的中点坐标
                                    x1 = 0.5 * (Polylines[i].GetPoint3dAt(0).X + Polylines[i].GetPoint3dAt(1).X);
                                    y1 = 0.5 * (Polylines[i].GetPoint3dAt(0).Y + Polylines[i].GetPoint3dAt(1).Y);
                                    z1 = 0.5 * (Polylines[i].GetPoint3dAt(0).Z + Polylines[i].GetPoint3dAt(1).Z);
                                    x2 = 0.5 * (Polylines[k].GetPoint3dAt(0).X + Polylines[k].GetPoint3dAt(1).X);
                                    y2 = 0.5 * (Polylines[k].GetPoint3dAt(0).Y + Polylines[k].GetPoint3dAt(1).Y);
                                    z2 = 0.5 * (Polylines[k].GetPoint3dAt(0).Z + Polylines[k].GetPoint3dAt(1).Z);
                                }
                            }
                        }
                    }
                    //这里是去除掉listOfTextCoords里数字，就是继电器接口处的内容，以防影响最后的结果
                    for (int i = 0; i < listOfTextWithCoords.Count; i++)
                    {
                        if (listOfTextWithCoords[i].Text == "30" || listOfTextWithCoords[i].Text == "85" || listOfTextWithCoords[i].Text == "86" || listOfTextWithCoords[i].Text == "87" || listOfTextWithCoords[i].Text == "87a")
                        {
                            listOfTextWithCoords.RemoveAt(i);
                            i = 0;
                        }
                    }

                    //线号
                    //删除特定线号和包含特定文本的文本对象
                    //List<DBText> flry = new List<DBText>();
                    //for (int i = 0; i < txts.Count; i++)
                    //{
                    //    if (txts[i].TextString == "0.5" || txts[i].TextString == "0.75")
                    //    {
                    //        txts.RemoveAt(i);
                    //        i = 0;
                    //    }
                    //    if (txts[i].TextString.Contains("FLRY") || txts[i].TextString.Contains("QVR"))
                    //    {

                    //        flry.Add(txts[i]);
                    //        txts.RemoveAt(i);
                    //        i = 0;
                    //    }
                    //}//这里对txts列表进行处理
                    //仿照对txts的处理进行处理，注意xianshu对应上面的flry，后续还没改动
                    List<TextWithCoordinates> xianshu = new List<TextWithCoordinates>();
                    for (int i = 0; i < listOfTextWithCoords.Count; i++)
                    {
                        if (listOfTextWithCoords[i].Text == "0.5" || listOfTextWithCoords[i].Text == "0.75")//这两个数字在线束信息附近，应该是为了避免影响文本内容直接删除了
                        {
                            listOfTextWithCoords.RemoveAt(i);
                            i = 0;
                        }
                        if (listOfTextWithCoords[i].Text.Contains("FLRY") || listOfTextWithCoords[i].Text.Contains("QVR"))
                        {

                            xianshu.Add(listOfTextWithCoords[i]);
                            listOfTextWithCoords.RemoveAt(i);
                            i = 0;
                        }
                    }
                    //检查一下线束处理后的列表与txts是否一样//这里最后的输出内容并不一样，原因在于txts文本包含了框外的内容，以及xshou新类型的列表包含了多行文本（一些数字）
                    //File.Delete("xshou.txt");
                    //foreach (var item in listOfTextWithCoords)
                    //{
                    //    File.AppendAllText("xshou.txt", $"Text: {item.Text})\n");
                    //}

                    //File.Delete("txts.txt");
                    //for (int i = 0; i < txts.Count; i++)
                    //{
                    //    File.AppendAllText("txts.txt", txts[i].TextString + "\r\n");
                    //}
                    /*                     
                    创建一个文本对象并将其添加到列表中。
                    定义两个起始点坐标，并初始化中间点坐标。
                    遍历线对象列表，找到起点坐标接近初始中间点的线对象，并将中间点坐标更新为这些线对象的终点坐标。
                     */
                    //DBText iiii = new DBText();
                    //flry.Add(iiii);
                    TextWithCoordinates iiiii = new TextWithCoordinates();
                    xianshu.Add(iiiii);

                    Point3d startpt1 = new Point3d(x1, y1, z1);
                    Point3d startpt2 = new Point3d(x2, y2, z2);
                    Point3d midpt = startpt1;
                    Point3d midpt1 = startpt2;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (Math.Abs(lines[i].StartPoint.Y - midpt.Y) < 0.01f && Math.Abs(lines[i].StartPoint.X - midpt.X) < 0.01f)
                        {
                            midpt = lines[i].EndPoint;
                            i = 1;
                        }
                    }
                    File.Delete("tl.txt");
                    //File.AppendAllText("tl.txt", "正极坐标" + " " + startpt1.X + startpt1.Y + "\r\n"/*, Encoding.Default*/);
                    //File.AppendAllText("tl.txt", "负极坐标" + " " + startpt2.X + startpt2.Y + "\r\n"/*, Encoding.Default*/);
                    /*
                    从mtxts列表中找到与midpt坐标接近的文本对象。
                    对于这些文本对象，进一步在flry列表中查找位置坐标接近的对象。
                    如果找到匹配的flry对象，则将其从列表中删除。
                     */
                    for (int k = 0; k < mtxts.Count; k++)
                    {
                        if (Math.Abs(midpt.X - mtxts[k].Location.X) < 4f && Math.Abs(midpt.Y - mtxts[k].Location.Y) < 4f)
                        {
                            //File.AppendAllText("tl.txt", "负极末端负载：" + mtxts[k].Text + " " + midpt.X + " " + midpt.Y + "\r\n");
                            double si = midpt.Y;
                            double tt = midpt.X;
                            //for (int ii = 0; ii < flry.Count; ii++)
                            //{
                            //    if (Math.Abs(flry[ii].Position.Y - si) < 10f && Math.Abs(flry[ii].Position.X - tt) < 3f)
                            //    {
                            //        //File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString + " " + flry[ii].Position.X + " " + flry[ii].Position.Y + "\r\n");
                            //        flry.RemoveAt(ii);
                            //        break;
                            //    }

                            //}
                            for (int ii = 0; ii < xianshu.Count; ii++)
                            {
                                if (Math.Abs(xianshu[ii].Coordinates.Y - si) < 10f && Math.Abs(xianshu[ii].Coordinates.X - tt) < 3f)
                                {
                                    //File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString + " " + flry[ii].Position.X + " " + flry[ii].Position.Y + "\r\n");
                                    xianshu.RemoveAt(ii);
                                    break;
                                }

                            }
                        }
                    }

                    List<Point3d> output = new List<Point3d>();
                    List<Point3d> outline = new List<Point3d>();
                    Point3d aaa = new Point3d(0, 0, 0);
                    outline.Add(aaa);
                    bool flagline = false;
                    Point3dCollection mnpt = new Point3dCollection();


                    Point3dCollection mpt = new Point3dCollection();//每次find中找到的新点的中间集合
                    Point3dCollection xpt = new Point3dCollection();//每次find的输入点集合
                    Point3dCollection outl = new Point3dCollection();//每次find的输出点集合

                    //MLNode<Point3d> aa=new MLNode<Point3d>();
                    //aa.Data = midpt1;
                    //aa.Childs[0] = aa;

                    //TeeNode<Point3d> bb = new TeeNode<Point3d>(midpt1);
                    //List<TeeNode<Point3d>> mmpt = new List<TeeNode<Point3d>>();
                    //Array.Resize(ref bb.child, bb.child.Length + 1);
                    //bb.child[0] = bb;
                    Point3dCollection cc = new Point3dCollection();//每次find的输入点集合
                    Hashtable ht = new Hashtable();
                    //ht.Add(midpt1, midpt1);
                    bool bre = false;


                    void findline()
                    {
                        /*                      
                        从mnpt列表中读取点。
                        检查这些点是否与lines列表中的线对象相关联，或者是否接近线的起点或终点。
                        如果点与线相关联或接近，则将相关的点和线对象的端点添加到相应的列表中，并从lines列表中移除这些线对象。
                        如果点不与任何线对象相关联，则将点添加到mpt列表中。
                         */
                        //遍历mnpt列表，每个元素是一个Point3d对象。
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            Point3d midpt3 = mnpt[ij];
                            flagline = false;
                            int dline = 0;
                            //遍历lines列表，每个元素是一个线对象。
                            for (int i = 0; i < lines.Count; i++)
                            {
                                //如果字典ht包含当前点midpt3，则获取与该点相关联的点midpt4。
                                if (ht.ContainsKey(midpt3))
                                {
                                    Point3d midpt4 = (Point3d)ht[midpt3];
                                    //检查点midpt3和midpt4是否在当前线对象lines[i]上
                                    if (GetPointIsInLine(lines[i].EndPoint, midpt3, midpt4, 0.5))
                                    {
                                        //更新midpt1为线的起点或终点（取决于点在起点还是终点）。
                                        midpt1 = lines[i].StartPoint;
                                        //将起点添加到mpt列表中
                                        mpt.Add(lines[i].StartPoint);
                                        //从lines列表中移除当前线对象。
                                        lines.RemoveAt(i);
                                        //重置循环索引i为0，以避免索引越界
                                        i = 0;
                                        //如果ht不包含midpt1，则将其添加到字典中，关联值为线的另一端点。
                                        if (!ht.ContainsKey(midpt1))
                                            ht.Add(midpt1, lines[i].EndPoint);

                                        //File.AppendAllText("tl.txt", "line上节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                        //将midpt1添加到output和outline列表中。
                                        output.Add(midpt1);
                                        outline.Add(midpt1);
                                        //增加dline计数
                                        dline++;
                                        flagline = true;
                                        //将flagline设置为true。
                                        bre = true;
                                    }
                                    //检查点与线起点或终点的接近度：
                                    //idpt3与线对象的起点或终点的Y和X坐标的绝对差值小于0.4
                                    if (GetPointIsInLine(lines[i].StartPoint, midpt3, midpt4, 0.5))
                                    {
                                        //更新midpt1为线的终点或起点。
                                        midpt1 = lines[i].EndPoint;
                                        //将终点或起点添加到mpt列表中
                                        mpt.Add(lines[i].EndPoint);
                                        //从lines列表中移除当前线对象。
                                        lines.RemoveAt(i);
                                        //重置循环索引i为0。
                                        i = 0;
                                        //如果ht不包含midpt1，则将其添加到字典中，关联值为midpt3。
                                        if (!ht.ContainsKey(midpt1))
                                            ht.Add(midpt1, lines[i].StartPoint);

                                        //File.AppendAllText("tl.txt", "line下节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                        output.Add(midpt1);
                                        outline.Add(midpt1);
                                        dline++;
                                        flagline = true;
                                        bre = true;
                                    }
                                }
                                /*                       
                                    检查线对象的起点是否接近某个中间点midpt3。
                                    如果接近，则将线对象的终点坐标添加到列表中，并从线对象列表中移除该线对象。
                                    更新相关的列表和字典，记录处理过程中的信息。
                                 */
                                //线对象lines[i]的起点坐标与中间点midpt3的坐标之间的绝对差值。
                                if (Math.Abs(lines[i].StartPoint.Y - midpt3.Y) < 0.4f && Math.Abs(lines[i].StartPoint.X - midpt3.X) < 0.4f)
                                {
                                    //将线对象lines[i]的终点坐标赋值给midpt1。
                                    midpt1 = lines[i].EndPoint;
                                    //将midpt1添加到mpt列表中。
                                    mpt.Add(lines[i].EndPoint);
                                    //使用lines.RemoveAt(i)从lines列表中移除当前的线对象。
                                    lines.RemoveAt(i);
                                    //将循环索引i重置为0。
                                    i = 0;
                                    //检查字典ht是否包含midpt1。如果不包含，则将midpt1作为键添加到字典中，并将midpt3作为值。
                                    if (!ht.ContainsKey(midpt1))
                                        ht.Add(midpt1, midpt3);
                                    //File.AppendAllText("tl.txt", "line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                    output.Add(midpt1);
                                    outline.Add(midpt1);
                                    dline++;
                                    flagline = true;
                                    bre = true;

                                }
                                /*                                 
                                    检查线对象的终点是否接近某个中间点midpt3。
                                    如果接近，则将线对象的起点坐标添加到列表中，并从线对象列表中移除该线对象。
                                    更新相关的列表和字典，记录处理过程中的信息。
                                 */
                                if (Math.Abs(lines[i].EndPoint.Y - midpt3.Y) < 0.4f && Math.Abs(lines[i].EndPoint.X - midpt3.X) < 0.4f)
                                {
                                    midpt1 = lines[i].StartPoint;
                                    mpt.Add(lines[i].StartPoint);
                                    lines.RemoveAt(i);
                                    i = 0;
                                    if (!ht.ContainsKey(midpt1))
                                        ht.Add(midpt1, midpt3);
                                    //File.AppendAllText("tl.txt", "line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                    output.Add(midpt1);
                                    outline.Add(midpt1);
                                    dline++;
                                    flagline = true;
                                    bre = true;
                                }

                            }
                            if (flagline == false)
                                mpt.Add(midpt3);
                        }

                        //for (int i = 0; i < mnpt.Count; i++)
                        //{
                        //    xpt.Add(mnpt[i]);
                        //}//xpt本轮输入
                        mnpt.Clear();
                        for (int i = 0; i < mpt.Count; i++)
                        {
                            mnpt.Add(mpt[i]);
                        }//mpt复制mnpt
                        mpt.Clear();
                        //for (int i = 0; i < outline.Count; i++)
                        //{
                        //    outl.Add(outline[i]);
                        //}//mpt复制mnpt
                        //outline.Clear();
                    }
                    void findgetline()
                    {
                        //这个循环遍历列表 mnpt，这个列表可能包含了一些 Point3d 对象
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            //获取对应的点
                            Point3d midpt3 = mnpt[ij];
                            //将符合要求的点添加到mpt
                            if (Math.Abs(midpt3.X - 20987.64) < 0.1 && Math.Abs(midpt3.Y + 661.85) < 0.1)//IF01的支路
                            {
                                Point3d ptl = new Point3d(20976.77, -661.85, 0);
                                mpt.Add(ptl);
                            }
                            if (Math.Abs(midpt3.X - 20807.46) < 0.1 && Math.Abs(midpt3.Y + 742.52) < 0.1)//ER07白点分割
                            {
                                Point3d ptl = new Point3d(20807.46, -736.6, 0);
                                mpt.Add(ptl);
                                Point3d pt2 = new Point3d(20807.46, -750.42, 0);
                                mpt.Add(pt2);
                            }
                            if (Math.Abs(midpt3.X - 21055.37) < 0.1 /*&& Math.Abs(midpt3.Y + 717.3) < 0.1*/)//IF保险丝支路
                            {
                                Point3d ptl = new Point3d(21043.27, midpt3.Y, 0);
                                mpt.Add(ptl);
                            }
                            if (Math.Abs(midpt3.X - 21085.35) < 0.1)//IP盒负载前的最后支路
                            {
                                Point3d ptl = new Point3d(21072.81, midpt3.Y, 0);
                                mpt.Add(ptl);
                            }
                        }
                    }

                    bool jdqf = false;
                    //识别与特定点相邻的线段，并根据条件修改或添加这些线段的起点或终点
                    void findjdqline()
                    {
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            Point3d midpt3 = mnpt[ij];
                            bool flagq = false;
                            //检查点 midpt3 是否存在于列表 jdqkg 中。
                            foreach (Point3d ee in jdqkg)
                            {
                                if (ee == midpt3)
                                    flagq = true;
                            }
                            //检查点 midpt3 是否存在于列表 jdqbx 中。
                            foreach (Point3d ee in jdqbx)
                            {
                                if (ee == midpt3)
                                    flagq = true;
                            }
                            flagline = false;
                            int dline = 0;
                            if (flagq)
                            {
                                //遍历 lines 列表，该列表包含线段对象。
                                for (int i = 0; i < lines.Count; i++)
                                {
                                    //检查每条线的起点或终点是否与 midpt3 相近，并且线段长度是否大于 0.5f
                                    if (Math.Abs(lines[i].StartPoint.Y - midpt3.Y) < 0.15f && Math.Abs(lines[i].StartPoint.X - midpt3.X) < 0.2f && lines[i].Length > 0.5f)
                                    {
                                        //将线段的终点或起点添加到 mpt 列表中。
                                        midpt1 = lines[i].EndPoint;
                                        mpt.Add(lines[i].EndPoint);
                                        //将线段从 lines 列表中移除
                                        lines.RemoveAt(i);
                                        //重置循环变量 i 为 0，以重新开始循环。
                                        i = 0;

                                        //File.AppendAllText("tl.txt", "继电器line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                        //添加点到 output 列表并记录处理的线段数量。
                                        output.Add(midpt1);
                                        dline++;
                                        flagline = true;
                                        bre = true;

                                    }
                                    //终点对比
                                    if (Math.Abs(lines[i].EndPoint.Y - midpt3.Y) < 0.15f && Math.Abs(lines[i].EndPoint.X - midpt3.X) < 0.2f && lines[i].Length > 0.5f)
                                    {
                                        midpt1 = lines[i].StartPoint;
                                        mpt.Add(lines[i].StartPoint);
                                        lines.RemoveAt(i);

                                        i = 0;
                                        //File.AppendAllText("tl.txt", "继电器line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                                        output.Add(midpt1);
                                        dline++;
                                        flagline = true;
                                        bre = true;
                                    }

                                }
                            }
                            if (flagline == false)
                                mpt.Add(midpt3);
                        }
                        //清空 mnpt
                        mnpt.Clear();
                        for (int i = 0; i < mpt.Count; i++)
                        {
                            mnpt.Add(mpt[i]);
                        }//mpt复制mnpt
                        mpt.Clear();

                        jdqf = false;
                    }

                    List<TextWithCoordinates> usedbxtxt = new List<TextWithCoordinates>();//改为TextWithCoordinates类型

                    void findtxt()
                    {
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            Point3d midpt3 = mnpt[ij];
                            for (int i = 0; i < listOfTextWithCoords.Count; i++)
                            {
                                string ss = "多媒体";
                                int oo = -1;
                                oo = listOfTextWithCoords[i].Text.IndexOf(ss);
                                string ss1 = "BCM";
                                bool oo1 = listOfTextWithCoords[i].Text.Equals(ss1);
                                //int oo1 = -1;
                                //oo1 = txts[i].TextString.LastIndexOf(ss1);
                                if (midpt3.X - 20870 > 0f)
                                {
                                    //y坐标可能范围过大
                                    //如果txts[i]的X坐标与中心点midpt3的X坐标的绝对距离小于15，并且Y坐标与中心点的Y坐标的绝对距离小于1.32，同时txts[i]的X坐标大于中心点的X坐标
                                    if (Math.Abs(listOfTextWithCoords[i].Coordinates.X - midpt3.X) < 15f && Math.Abs(listOfTextWithCoords[i].Coordinates.Y - midpt3.Y) < 1.32f && (listOfTextWithCoords[i].Coordinates.X > midpt3.X))
                                    {
                                        string s = "To";
                                        string er = "IP";
                                        int l = -1;
                                        int k = -1;
                                        k = listOfTextWithCoords[i].Text.IndexOf(s);
                                        l = listOfTextWithCoords[i].Text.IndexOf(er);
                                        if (k >= 0 && l >= 0)
                                        {
                                            Point3d bx11 = new Point3d(midpt3.X + 8.6, midpt3.Y, midpt3.Z);
                                            mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, bx11);
                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + bx11.X + " " + bx11.Y */+ "\r\n");
                                        }
                                        else if (k >= 0 && l < 0)
                                        {
                                            Point3d bx11 = new Point3d(midpt3.X + 10, midpt3.Y, midpt3.Z);
                                            mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, bx11);
                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + bx11.X + " " + bx11.Y*/ + "\r\n");
                                        }
                                        //K小于0的情况
                                        else
                                        {
                                            if (oo < 0)
                                                mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, listOfTextWithCoords[i].Coordinates);
                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + txts[i].Position.X + " " + txts[i].Position.Y */+ "\r\n");
                                            double si = listOfTextWithCoords[i].Coordinates.Y;
                                            double tt = listOfTextWithCoords[i].Coordinates.X - 23;
                                            //for (int ii = 0; ii < flry.Count; ii++)
                                            //{
                                            //    if (ii == flry.Count - 1)
                                            //        break;

                                            //    if (Math.Abs(flry[ii].Position.Y - si) < 2f && Math.Abs(flry[ii].Position.X - tt) < 6f && (flry[ii].Position.Y > si || si - flry[ii].Position.Y < 0.12f))
                                            //    {

                                            //        File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y*/ + "\r\n");
                                            //        flry.RemoveAt(ii);
                                            //        break;
                                            //    }

                                            //}
                                            for (int ii = 0; ii < xianshu.Count; ii++)
                                            {
                                                if (ii == xianshu.Count - 1)
                                                    break;

                                                if (Math.Abs(xianshu[ii].Coordinates.Y - si) < 2f && Math.Abs(xianshu[ii].Coordinates.X - tt) < 6f && (xianshu[ii].Coordinates.Y > si || si - xianshu[ii].Coordinates.Y < 0.12f))
                                                {

                                                    File.AppendAllText("tl.txt", "线束信息：" + " " + xianshu[ii].Text /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y*/ + "\r\n");
                                                    xianshu.RemoveAt(ii);
                                                    break;
                                                }

                                            }
                                        }
                                        //这部分有定值，检查一下能不能改
                                        for (int ii = 0; ii < usedbxtxt.Count; ii++)
                                        {
                                            if ((Math.Abs(usedbxtxt[ii].Coordinates.Y - listOfTextWithCoords[i].Coordinates.Y - 2.23) < 1f && Math.Abs(listOfTextWithCoords[i].Coordinates.X - usedbxtxt[ii].Coordinates.X - 111.75) < 5f) || (Math.Abs(usedbxtxt[ii].Coordinates.Y - listOfTextWithCoords[i].Coordinates.Y - 1.46) < 2f && Math.Abs(listOfTextWithCoords[i].Coordinates.X - usedbxtxt[ii].Coordinates.X - 43.09) < 4f))
                                            {
                                                File.AppendAllText("tl.txt", "前置保险：" + " " + usedbxtxt[ii].Text + "\r\n");
                                                break;
                                            }
                                        }
                                        if (oo1)
                                        {
                                            File.AppendAllText("tl.txt", "前置保险：" + " " + usedbxtxt[1].Text + "\r\n");
                                        }

                                        for (int ii = 0; ii < listjdq.Count; ii++)
                                        {
                                            if ((Math.Abs(listOfTextWithCoords[i].Coordinates.Y - listjdq[ii].Coordinates.Y - 2.62) < 1.5f && Math.Abs(listOfTextWithCoords[i].Coordinates.X - listjdq[ii].Coordinates.X - 77.03) < 5f))
                                            {
                                                File.AppendAllText("tl.txt", "同支路继电器线圈：" + " " + listjdq[ii].Text + "线圈端" + "\r\n");
                                                break;
                                            }
                                        }
                                        flagline = false;
                                        bre = true;
                                        listOfTextWithCoords.RemoveAt(i);//为了去掉末端重复，移除txt内容
                                    }
                                }
                                else
                                {

                                    if (Math.Abs(listOfTextWithCoords[i].Coordinates.X - midpt3.X) < 4f && Math.Abs(listOfTextWithCoords[i].Coordinates.Y - midpt3.Y) < 1.2f && (listOfTextWithCoords[i].Coordinates.X > midpt3.X))
                                    {

                                        string s = "To";
                                        string er = "IP";
                                        int l = -1;
                                        int k = -1;
                                        k = listOfTextWithCoords[i].Text.IndexOf(s);
                                        l = listOfTextWithCoords[i].Text.IndexOf(er);
                                        if (k >= 0 && l >= 0)
                                        {
                                            Point3d bx11 = new Point3d(midpt3.X + 8.6, midpt3.Y, midpt3.Z);
                                            mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, bx11);
                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + bx11.X + " " + bx11.Y*/ + "\r\n");
                                            double si = listOfTextWithCoords[i].Coordinates.Y;
                                            double tt = listOfTextWithCoords[i].Coordinates.X - 10.2;
                                            //for (int ii = 0; ii < flry.Count; ii++)
                                            //{
                                            //    if (Math.Abs(flry[ii].Position.Y - si) < 2f && Math.Abs(flry[ii].Position.X - tt) < 1f)
                                            //    {
                                            //        File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                            //        flry.RemoveAt(ii);
                                            //        break;
                                            //    }

                                            //}
                                            for (int ii = 0; ii < xianshu.Count; ii++)
                                            {
                                                if (Math.Abs(xianshu[ii].Coordinates.Y - si) < 2f && Math.Abs(xianshu[ii].Coordinates.X - tt) < 1f)
                                                {
                                                    File.AppendAllText("tl.txt", "线束信息：" + " " + xianshu[ii].Text /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                                    xianshu.RemoveAt(ii);
                                                    break;
                                                }

                                            }
                                        }
                                        else if (k >= 0 && l < 0)
                                        {
                                            Point3d bx11 = new Point3d(midpt3.X + 10, midpt3.Y, midpt3.Z);
                                            mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, bx11);
                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + bx11.X + " " + bx11.Y*/ + "\r\n");
                                            double si = listOfTextWithCoords[i].Coordinates.Y;
                                            double tt = listOfTextWithCoords[i].Coordinates.X - 10.2;
                                            //for (int ii = 0; ii < flry.Count; ii++)
                                            //{
                                            //    if (Math.Abs(flry[ii].Position.Y - si) < 2f && Math.Abs(flry[ii].Position.X - tt) < 1f)
                                            //    {
                                            //        File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString/* + " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                            //        flry.RemoveAt(ii);
                                            //        break;
                                            //    }

                                            //}
                                            for (int ii = 0; ii < xianshu.Count; ii++)
                                            {
                                                if (Math.Abs(xianshu[ii].Coordinates.Y - si) < 2f && Math.Abs(xianshu[ii].Coordinates.X - tt) < 1f)
                                                {
                                                    File.AppendAllText("tl.txt", "线束信息：" + " " + xianshu[ii].Text/* + " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                                    xianshu.RemoveAt(ii);
                                                    break;
                                                }

                                            }
                                        }
                                        else
                                        {
                                            mnpt.RemoveAt(ij);
                                            mnpt.Insert(ij, listOfTextWithCoords[i].Coordinates);

                                            File.AppendAllText("tl.txt", "连接负载：" + " " + listOfTextWithCoords[i].Text /*+ " " + txts[i].Position.X + " " + txts[i].Position.Y*/ + "\r\n");
                                            double si = listOfTextWithCoords[i].Coordinates.Y;
                                            double tt = listOfTextWithCoords[i].Coordinates.X - 10.2;
                                            //for (int ii = 0; ii < flry.Count; ii++)
                                            //{
                                            //    if (Math.Abs(flry[ii].Position.Y - si) < 2f && Math.Abs(flry[ii].Position.X - tt) < 1f)
                                            //    {
                                            //        File.AppendAllText("tl.txt", "线束信息：" + " " + flry[ii].TextString /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                            //        flry.RemoveAt(ii);
                                            //        break;
                                            //    }

                                            //}
                                            for (int ii = 0; ii < xianshu.Count; ii++)
                                            {
                                                if (Math.Abs(xianshu[ii].Coordinates.Y - si) < 2f && Math.Abs(xianshu[ii].Coordinates.X - tt) < 1f)
                                                {
                                                    File.AppendAllText("tl.txt", "线束信息：" + " " + xianshu[ii].Text /*+ " " + flry[ii].Position.X + " " + flry[ii].Position.Y */+ "\r\n");
                                                    xianshu.RemoveAt(ii);
                                                    break;
                                                }

                                            }
                                        }

                                        for (int ii = 0; ii < usedbxtxt.Count; ii++)
                                        {
                                            if (Math.Abs(usedbxtxt[ii].Coordinates.Y - listOfTextWithCoords[i].Coordinates.Y - 1.51) < 1f && Math.Abs(listOfTextWithCoords[i].Coordinates.X - usedbxtxt[ii].Coordinates.X - 30.4) < 1f)
                                            {
                                                File.AppendAllText("tl.txt", "前置保险：" + " " + usedbxtxt[ii].Text + "\r\n");

                                                break;
                                            }
                                        }
                                        flagline = false;
                                        bre = true;
                                        listOfTextWithCoords.RemoveAt(i);//为了去掉末端重复，移除txt内容

                                    }
                                }

                            }
                        }

                    }


                    //在一个列表arcs中查找与列表mnpt中的点匹配的圆弧，并根据匹配结果更新mnpt列表
                    void findarc()
                    {
                        //遍历mnpt列表中的每个点
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            //flagline = false;
                            //当前遍历到的点赋值给midpt3变量
                            Point3d midpt3 = mnpt[ij];
                            //遍历arcs列表中的每个圆弧。
                            for (int i = 0; i < arcs.Count; i++)
                            {
                                //检查圆弧的起点或终点是否与midpt3非常接近（接近程度由小于0.1f定义）并且圆弧要接近是半圆
                                if (Math.Abs(arcs[i].StartPoint.X - midpt3.X) < 0.1f && Math.Abs(arcs[i].StartPoint.Y - midpt3.Y) < 0.1f && Math.Abs(arcs[i].TotalAngle - 3.1415926) < 0.01f)
                                //if (Math.Abs(Math.Sqrt((arcs[i].Center.X - midpt3.X)*(arcs[i].Center.X - midpt3.X)+ (arcs[i].Center.Y - midpt3.Y)* (arcs[i].Center.Y - midpt3.Y) )- arcs[i].Radius)<0.1f)
                                {
                                    //File.AppendAllText("tl.txt",/* o[ij]+*/  "圆弧：" +  " " + midpt3.X + " " + midpt3.Y + "\r\n");
                                    //mpt.Add(arcs[i].EndPoint);
                                    //mnpt.RemoveAt(ij);
                                    mnpt.Insert(ij, arcs[i].EndPoint);
                                    arcs.RemoveAt(i);
                                    i = 0;
                                    output.Add(midpt1);
                                    bre = true;
                                    //flagline = true;

                                }
                                //终点检测
                                if (Math.Abs(arcs[i].EndPoint.X - midpt3.X) < 0.1f && Math.Abs(arcs[i].EndPoint.Y - midpt3.Y) < 0.1f && Math.Abs(arcs[i].TotalAngle - 3.1415926) < 0.01f)
                                {
                                    //File.AppendAllText("tl.txt",/* o[ij]+*/  "圆弧：" + " " + midpt3.X + " " + midpt3.Y + "\r\n");
                                    //mpt.Add(arcs[i].StartPoint);
                                    //mnpt.RemoveAt(ij);
                                    mnpt.Insert(ij, arcs[i].StartPoint);
                                    arcs.RemoveAt(i);
                                    i = 0;
                                    output.Add(midpt1);
                                    //flagline = true;
                                    bre = true;
                                }
                            }
                            //if (flagline == false)
                            //    mpt.Add(midpt3);
                        }
                        //mnpt.Clear();
                        //for (int i = 0; i < mpt.Count; i++)
                        //{
                        //    mnpt.Add(mpt[i]);
                        //}//mpt复制mnpt
                        //mpt.Clear();
                    }
                    //int []o = new int[10];//保险丝级数
                    //for(int opopo=0; opopo<o.Length; opopo++)
                    //{
                    //    o[opopo] = 0;
                    //}
                    int obx = 0;

                    Point3d mpp = new Point3d(21055.8, -717.3, 0);
                    //在一个列表bxs中查找与mnpt列表中的点接近的保险丝
                    void findbx()
                    {
                        flagline = false;
                        // //遍历mnpt列表中的每个点
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            Point3d midpt3 = mnpt[ij];
                            //遍历bxs列表中的每个保险丝
                            for (int i = 0; i < bxs.Count; i++)
                            {
                                //通过IndexOf方法查找特定字符串在bxtxt[i].TextString中的位置
                                //通过Contains方法判断bxtxt[i].TextString是否包含特定的字符串，如"MF", "IF", "ESB", "EF"，并根据结果设置变量obx的值。//把这部分的bxtxt全部改为了listbxs
                                int fbx = 0;
                                string ss = "啖";
                                int oo = -1;
                                oo = listbxs[i].Text.IndexOf(ss);
                                string s = "IF10";
                                int o = -1;
                                o = listbxs[i].Text.IndexOf(s);
                                string s1 = "IF15";
                                int o1 = -1;
                                o1 = listbxs[i].Text.IndexOf(s1);
                                string s2 = "IF19";
                                int o2 = -1;
                                o2 = listbxs[i].Text.IndexOf(s2);
                                string s3 = "EF04";
                                int o3 = -1;
                                o3 = listbxs[i].Text.IndexOf(s3);
                                string s4 = "EF03";
                                int o4 = -1;
                                o4 = listbxs[i].Text.IndexOf(s4);
                                if (listbxs[i].Text.Contains("MF"))
                                    obx = 1;
                                if (listbxs[i].Text.Contains("IF"))
                                {
                                    obx = 2;
                                    fbx = 1;
                                }
                                if (listbxs[i].Text.Contains("ESB") || listbxs[i].Text.Contains("EF"))
                                {
                                    obx = 2;
                                    fbx = 2;
                                }
                                //判断保险丝的坐标与midpt3的坐标差的绝对值是否在一定的范围内
                                if (Math.Abs(bxs[i].X - midpt3.X) < 4f && Math.Abs(bxs[i].Y - midpt3.Y) < 1f)//可能过宽
                                {
                                    //o[ij]++;
                                    //将midpt3添加到output列表
                                    output.Add(midpt3);
                                    flagline = true;
                                    bre = true;
                                    Point3d bx11 = new Point3d(bxe[i].X + Global.circleRadius, bxe[i].Y, bxe[i].Z);//定位到另一端的端点，这里是个定值0.43 //已改
                                    //如果fbx的值为1或2，记录上一级保险丝的文本到文件
                                    File.AppendAllText("tl.txt", "第" + obx + "级保险：" + " " + listbxs[i].Text /*+ " " + bx11.X + " " + bx11.Y */+ "\r\n");
                                    if (fbx == 1)
                                        File.AppendAllText("tl.txt", "上一级保险：" + " " + usedbxtxt[1].Text + "\r\n");
                                    if (fbx == 2)
                                        File.AppendAllText("tl.txt", "上一级保险：" + " " + usedbxtxt[0].Text + "\r\n");
                                    //mpt.Add(bx11);
                                    //更新mnpt列表，移除当前点并插入新的点bx11。
                                    mnpt.RemoveAt(ij);
                                    mnpt.Insert(ij, bx11);
                                    //bxs.RemoveAt(i);
                                    //bxe.RemoveAt(i);
                                    //File.AppendAllText("tt.txt", bxtxt[i].TextString + "\r\n");
                                    usedbxtxt.Add(listbxs[i]);
                                    //根据找到的特定字符串索引，创建新的DBText对象并添加到usedbxtxt列表。
                                    if (o >= 0)
                                    {
                                        for (int opp = 1; opp < 4; opp++)
                                        {
                                            TextWithCoordinates tet = new TextWithCoordinates();                                           
                                            tet.Text = listbxs[i].Text;
                                            Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y + 2.88 * opp, listbxs[i].Coordinates.Z);
                                            tet.Coordinates = ttt;
                                            usedbxtxt.Add(tet);
                                        }
                                    }
                                    if (o1 >= 0)
                                    {
                                        for (int opp = 1; opp < 6; opp++)
                                        {
                                            TextWithCoordinates tet = new TextWithCoordinates();
                                            tet.Text = listbxs[i].Text;
                                            Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y + 2.88 * opp, listbxs[i].Coordinates.Z);
                                            tet.Coordinates = ttt;
                                            usedbxtxt.Add(tet);
                                        }
                                    }
                                    if (o2 >= 0)
                                    {
                                        for (int opp = 1; opp < 7; opp++)
                                        {
                                            TextWithCoordinates tet = new TextWithCoordinates();
                                            tet.Text = listbxs[i].Text;
                                            Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y + 2.88 * opp, listbxs[i].Coordinates.Z);
                                            tet.Coordinates = ttt;
                                            usedbxtxt.Add(tet);
                                        }
                                    }
                                    if (o3 >= 0)
                                    {
                                        for (int opp = 1; opp < 4; opp++)
                                        {
                                            TextWithCoordinates tet = new TextWithCoordinates();
                                            tet.Text = listbxs[i].Text;
                                            Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y - 2.88 * opp, listbxs[i].Coordinates.Z);
                                            tet.Coordinates = ttt;
                                            usedbxtxt.Add(tet);
                                        }
                                    }
                                    if (o4 >= 0)
                                    {
                                        for (int opp = 1; opp < 4; opp++)
                                        {
                                            TextWithCoordinates tet = new TextWithCoordinates();
                                            tet.Text = listbxs[i].Text;
                                            Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y + 2.88 * opp * 4, listbxs[i].Coordinates.Z);
                                            tet.Coordinates = ttt;
                                            usedbxtxt.Add(tet);
                                        }
                                        for (int opp = 1; opp < 3; opp++)
                                        {
                                            if (opp == 1)
                                            {
                                                TextWithCoordinates tet = new TextWithCoordinates();
                                                tet.Text = listbxs[i].Text;
                                                Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y - 2.88, listbxs[i].Coordinates.Z);
                                                tet.Coordinates = ttt;
                                                usedbxtxt.Add(tet);
                                            }
                                            else
                                            {
                                                TextWithCoordinates tet = new TextWithCoordinates();
                                                tet.Text = listbxs[i].Text;
                                                Point3d ttt = new Point3d(listbxs[i].Coordinates.X, listbxs[i].Coordinates.Y + 2.88 * 15, listbxs[i].Coordinates.Z);
                                                tet.Coordinates = ttt;
                                                usedbxtxt.Add(tet);
                                            }
                                        }
                                    }
                                    //bxtxt.RemoveAt(i);
                                    //DBText text = new DBText();
                                    //text.TextString = "啖!";

                                    //bxtxt.Insert(i, text);

                                    //不知道为什么删除，但模仿着去删除修改后的代码
                                    listbxs.RemoveAt(i);
                                    TextWithCoordinates text01 = new TextWithCoordinates();
                                    text01.Text = "啖!";

                                    listbxs.Insert(i, text01);

                                    break;
                                }



                            }
                            //if (flagline == false)
                            //    mpt.Add(midpt3);
                        }
                        
                        //mnpt.Clear();
                        //for (int i = 0; i < mpt.Count; i++)
                        //{
                        //    mnpt.Add(mpt[i]);
                        //}//mpt复制mnpt
                        //mpt.Clear();
                    }                 
                    //void jdqline(Point3d midpt9,Point3d midpt8)
                    //{                            
                    //        flagline = false;
                    //        int dline = 0;
                    //        for (int i = 0; i < lines.Count; i++)
                    //        {
                    //            if (Math.Abs(lines[i].StartPoint.Y - midpt9.Y) < 0.15f && Math.Abs(lines[i].StartPoint.X - midpt9.X) < 0.2f && lines[i].Length > 0.5f)
                    //            {

                    //                midpt1 = lines[i].EndPoint;
                    //                midpt8 = midpt1;
                    //                lines.RemoveAt(i);
                    //                i = 0;
                    //                File.AppendAllText("tl.txt", "继电器line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                    //                output.Add(midpt1);
                    //                dline++;
                    //                flagline = true;
                    //            bre = true;

                    //        }
                    //            if (Math.Abs(lines[i].EndPoint.Y - midpt9.Y) < 0.15f && Math.Abs(lines[i].EndPoint.X - midpt9.X) < 0.2f && lines[i].Length > 0.5f)
                    //            {
                    //                midpt1 = lines[i].StartPoint;
                    //                midpt8 = midpt1;
                    //                //mpt.Add(lines[i].StartPoint);
                    //                lines.RemoveAt(i);                                    
                    //                i = 0;
                    //                File.AppendAllText("tl.txt", "继电器line节点：" + dline + " " + " " + midpt1.X + " " + midpt1.Y + "\r\n");
                    //                output.Add(midpt1);
                    //                dline++;
                    //                flagline = true;
                    //            bre = true;
                    //        }

                    //        }


                    //}
                    //在一个列表中查找与mnpt列表中的点接近的接地开关或保险丝，并根据匹配结果更新mnpt列表
                    void findjdq()
                    {
                        //遍历mnpt列表中的每个点。
                        for (int ij = 0; ij < mnpt.Count; ij++)
                        {
                            flagline = false;
                            Point3d midpt3 = mnpt[ij];
                            //遍历与接地开关相关的列表。
                            for (int i = 0; i < jdq1.Count; i++)
                            {
                                //判断接地开关的第一个点（索引为3 * i）与midpt3的坐标差的绝对值是否在0.05以内
                                if (Math.Abs(jdqkg[3 * i + 1].X - midpt3.X) < 0.05f && Math.Abs(jdqkg[3 * i + 1].Y - midpt3.Y) < 0.05f)
                                {
                                    //File.AppendAllText("tl.txt", jdqtxt[i].TextString + "开关 " + mnpt[ij].X + " " + mnpt[ij].Y + "\r\n");
                                    //添加接地开关的第一个点到mpt列表
                                    mpt.Add(jdqkg[3 * i]);
                                    //添加当前点mnpt[ij]到output列表
                                    output.Add(mnpt[ij]);
                                    flagline = true;
                                    bre = true;
                                    Point3d midpt7 = new Point3d(0, 0, 0);

                                    //jdqline(jdqkg[3 * i + 1],midpt7);
                                    //mpt.Add(midpt7);
                                    //jdqline(jdqkg[3 * i + 2], midpt7);
                                    //mpt.Add(midpt7);

                                    jdqf = true;

                                }
                                //接地开关的第二个点（索引为3 * i + 1）与midpt3的坐标差的绝对值是否在0.05以内。
                                if (Math.Abs(jdqkg[3 * i].X - midpt3.X) < 0.05f && Math.Abs(jdqkg[3 * i].Y - midpt3.Y) < 0.05f)
                                {
                                    //File.AppendAllText("tl.txt",   jdqtxt[i].TextString + "开关 " + mnpt[ij].X + " " + mnpt[ij].Y + "\r\n");
                                    //检查接地开关的文本描述中是否包含字符串"IR"
                                    string s = "IR";
                                    int k = -1;
                                    k = listjdq[i].Text.IndexOf(s);
                                    //根据是否包含"IR"，决定是否添加接地开关的第三个点到mpt列表
                                    if (k < 0)
                                    { mpt.Add(jdqkg[3 * i + 2]); }
                                    mpt.Add(jdqkg[3 * i + 1]);
                                    //添加接地开关的第二个点和一个新的点eee到mpt列表
                                    Point3d eee = new Point3d(midpt3.X - 9.5, midpt3.Y, midpt3.Z);
                                    mpt.Add(eee);
                                    //添加当前点mnpt[ij]到output列表
                                    output.Add(mnpt[ij]);
                                    flagline = true;
                                    bre = true;
                                    Point3d midpt7 = new Point3d(0, 0, 0);


                                    //jdqline(jdqkg[3 * i + 1],midpt7);
                                    //mpt.Add(midpt7);
                                    //jdqline(jdqkg[3 * i + 2], midpt7);
                                    //mpt.Add(midpt7);

                                    jdqf = true;

                                }
                                //判断保险丝的点（索引为2 * i）与midpt3的坐标差的绝对值是否在0.05以内
                                if (Math.Abs(jdqbx[2 * i].X - midpt3.X) < 0.05f && Math.Abs(jdqbx[2 * i].Y - midpt3.Y) < 0.05f)
                                {
                                    //File.AppendAllText("tl.txt", jdqtxt[i].TextString + "保险 " + mnpt[ij].X + " " + mnpt[ij].Y + "\r\n");
                                    output.Add(mnpt[ij]);
                                    flagline = true;
                                    bre = true;
                                    //Point3d midpt7 = new Point3d(0, 0, 0);
                                    //jdqline(jdqkg[2 * i + 1], midpt7);
                                    //mpt.Add(midpt7);
                                    mpt.Add(jdqbx[2 * i + 1]);


                                    jdqf = true;
                                }
                            }
                            if (flagline == false)
                                mpt.Add(midpt3);
                        }
                        mnpt.Clear();
                        for (int i = 0; i < mpt.Count; i++)
                        {
                            mnpt.Add(mpt[i]);
                        }//mpt复制mnpt
                        mpt.Clear();
                    }




                    Point3d flagpt = new Point3d(0, 0, 0);
                    Point3dCollection flagptc = new Point3dCollection();
                    //do
                    //{
                    //    if (Math.Abs(flagpt.X - midpt1.X) < 0.01f && Math.Abs(flagpt.Y - midpt1.Y) < 0.01f)
                    //    {
                    //        break;
                    //    }

                    //    flagpt = midpt1;

                    //    //flagptc = midptc;
                    //    findline();
                    //    findtxt();
                    //    findbx();
                    //    //int ppp = 0;
                    //    //for (int ptc = 0; ptc < midptc.Count; ptc++)
                    //    //{
                    //    //    if (Math.Abs(flagptc[ptc].X - midptc[ptc].X) < 0.01f && Math.Abs(flagptc[ptc].Y - midptc[ptc].Y) < 0.01f)
                    //    //        ppp++;
                    //    //}
                    //    //if (ppp == midptc.Count)
                    //    //    break;
                    //} while (true);
                    //将midpt1这个Point3d对象添加到mnpt列表中
                    mnpt.Add(midpt1);
                    //创建一个新的Point3d对象，并将其坐标初始化为(0, 0, 0)，赋值给flagpt。
                    flagpt = new Point3d(0, 0, 0);
                    int jj = 0;
                    //下面相当于整个代码流程
                    do
                    {
                        jj++;
                        bre = false;
                        //if (Math.Abs(flagpt.X - midpt1.X) < 0.01f && Math.Abs(flagpt.Y - midpt1.Y) < 0.01f)
                        //{
                        //    flagptc.RemoveAt(0);
                        //}
                        //File.AppendAllText("tl.txt", "循环轮  " +jj+ "\r\n");


                        if (jdqf == true)
                        {
                            findjdqline();
                        }
                        else if (jdqf == false)
                        {
                            findline();
                        }
                        findgetline();
                        findtxt();
                        findbx();
                        findjdq();
                        findarc();
                        if (bre == false)
                            break;
                    } while (true);
                    //File.Delete("tes.txt");
                    //foreach (DBText i in usedbxtxt)
                    //{


                    //    File.AppendAllText("tes.txt", "zhixian1：" + i.TextString + i.Position.X+i.Position.Y+" " + "\r\n");
                    //}
                    //File.Delete("usedbx.txt");
                    //foreach (var item in usedbxtxt)
                    //{
                    //    File.AppendAllText("usedbx.txt", $"Text: {item.Text})\n");
                    //}
                }
            }
        }


        public void toTxtToolStripMenuItem_Click(object sender, EventArgs e)//重点修改，要么就是比较多内容，总线型
        {
            TransactionManager tm = database.TransactionManager;
            using (Transaction tr = tm.StartTransaction())
            {
                using (BlockTableRecord modelSpace = (BlockTableRecord)database.CurrentSpaceId.GetObject(OpenMode.ForRead))
                {
                    #region 导入图元
                    List<Arc> arcs = new List<Arc>();
                    List<DBText> txts = new List<DBText>();
                    List<MText> mtxts = new List<MText>();
                    List<Circle> cirs = new List<Circle>();
                    List<my_line> mylines = new List<my_line>();
                    List<Line> lines = new List<Line>();
                    List<TextWithCoordinates> bxtxt = new List<TextWithCoordinates>();//保险丝信息
                    List<Point3d> bxs = new List<Point3d>();
                    List<Point3d> bxe = new List<Point3d>();
                    List<Point3d> jdq1 = new List<Point3d>();
                    List<TextWithCoordinates> alltxts = new List<TextWithCoordinates>();
                    List<Line> jdqr = new List<Line>();
                    List<Point3d> jdqkg = new List<Point3d>();
                    List<Point3d> jdqbx = new List<Point3d>();
                    List<DBText> jdqtxt = new List<DBText>();//保险丝信息
                    List<BlockReference> blocks = new List<BlockReference>();
                    List<Teigha.DatabaseServices.Polyline> Polylines = new List<Teigha.DatabaseServices.Polyline>();
                    File.Delete("ttp.txt");
                    File.AppendAllText("ttp.txt", "dhh" + "\r\n"/*, Encoding.Default*/);

                    if (GlobalEntityData.SelectedEntityId != ObjectId.Null)
                    {
                        using (Entity ent = (Entity)GlobalEntityData.SelectedEntityId.GetObject(OpenMode.ForRead))
                        {
                            if (ent != null)
                            {
                                Type entityType = GlobalEntityData.SelectedEntity.GetType();
                                //MessageBox.Show(entityType.ToString(), "类型");
                                if (ent is Line line)
                                {
                                    //lineEntity = line;
                                    Point3d startPoint = line.StartPoint;
                                    Point3d endPoint = line.EndPoint;
                                    //MessageBox.Show(startPoint.ToString(), "起点");
                                    //MessageBox.Show(endPoint.ToString(), "终点");
                                }
                            }
                            else
                            {
                                MessageBox.Show("未选中实体", "提示");
                            }
                        }
                    }

                    leftx = Global.mlist.First();
                    double lefty = Global.mlist[1];
                    rightx = Global.mlist[2];
                    double righty = Global.mlist[3];
                    bool flaggetli = false;
                    /*                    File.Delete("acPolyline.txt");
                                        File.Delete("debug1.txt");
                                        File.Delete("blocks.txt");
                                        File.Delete("cycle.txt");
                                        File.Delete("acText.txt");
                                        File.Delete("acMText.txt");*/
                    foreach (ObjectId oid in modelSpace)
                    {
                        DBObject dbobj = tr.GetObject(oid, OpenMode.ForRead);
                        if (dbobj is Entity)
                        {

                            Entity entity = dbobj as Entity;

                            string enttype = entity.GetRXClass().Name;
                            File.AppendAllText("ttc.txt", enttype + "\r\n"/*, Encoding.Default*/);
                            if (enttype == "AcDbMText")
                            {
                                MText acMText = entity as MText;
                                if (acMText.Location.X > leftx && acMText.Location.X < rightx && acMText.Location.Y < lefty && acMText.Location.Y > righty && acMText.Text.Length > 0)
                                {
                                    alltxts.Add(new TextWithCoordinates(acMText.Text, acMText.Location));
                                    mtxts.Add(acMText);
                                    //File.AppendAllText("acMText.txt", acMText.Text + acMText.Location + " " + acMText.TextHeight + "\r\n");
                                }
                            }
                            if (enttype == "AcDbArc")
                            {
                                Arc acArc = entity as Arc;
                                if (acArc.Center.X > leftx && acArc.Center.X < rightx && acArc.Center.Y < lefty && acArc.Center.Y > righty)
                                {
                                    arcs.Add(acArc);
                                }
                            }
                            if (enttype == "AcDbText")
                            {
                                DBText acText = entity as DBText;
                                if (acText.Position.X > leftx && acText.Position.X < rightx && acText.Position.Y < lefty && acText.Position.Y > righty)
                                {
                                    txts.Add(acText);
                                    alltxts.Add(new TextWithCoordinates(acText.TextString, acText.Position));
                                    //File.AppendAllText("acText.txt", acText.TextString + " " + acText.Height + "\r\n");
                                }
                            }
                            if (enttype == "AcDbCircle")
                            {
                                Circle acCircle = entity as Circle;
                                if (acCircle.Center.X > leftx && acCircle.Center.X < rightx && acCircle.Center.Y < lefty && acCircle.Center.Y > righty)
                                {
                                    cirs.Add(acCircle);
                                    //File.AppendAllText("cycle.txt", "半径" + acCircle.Radius + "圆心" + acCircle.Center + "\r\n");
                                }
                            }
                            if (enttype == "AcDbLine")
                            {
                                Line acLine = entity as Line;
                                if (acLine.Length > 2.03 && acLine.StartPoint.X > leftx && acLine.StartPoint.X < rightx && acLine.EndPoint.X > leftx && acLine.EndPoint.X < rightx
                                    && acLine.StartPoint.Y < lefty && acLine.StartPoint.Y > righty && acLine.EndPoint.Y < lefty && acLine.EndPoint.Y > righty)//原来2.3
                                {
                                    if ((acLine.StartPoint.X < acLine.EndPoint.X && Math.Abs(acLine.StartPoint.Y - acLine.EndPoint.Y) < 2.0f) ||
                                       (acLine.StartPoint.Y > acLine.EndPoint.Y && Math.Abs(acLine.StartPoint.X - acLine.EndPoint.X) < 2.0f))
                                    {
                                        lines.Add(acLine);
                                        my_line ml;
                                        ml.line = acLine;
                                        ml.visit = false;
                                        mylines.Add(ml);
                                    }
                                    else
                                    {
                                        Line newLine = new Line(acLine.EndPoint, acLine.StartPoint);
                                        lines.Add(newLine);
                                        my_line ml;
                                        ml.line = newLine;
                                        ml.visit = false;
                                        mylines.Add(ml);

                                    }
                                }

                            }
                            if (enttype == "AcDbPolyline")
                            {
                                Teigha.DatabaseServices.Polyline acPolyline = entity as Teigha.DatabaseServices.Polyline;
                                if (acPolyline.StartPoint.X > leftx && acPolyline.StartPoint.X < rightx && acPolyline.EndPoint.X > leftx && acPolyline.EndPoint.X < rightx
                                    && acPolyline.StartPoint.Y < lefty && acPolyline.StartPoint.Y > righty && acPolyline.EndPoint.Y < lefty && acPolyline.EndPoint.Y > righty)
                                {
                                    if (acPolyline.Closed && acPolyline.NumberOfVertices == 4)
                                    {
                                        Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();
                                        List<Point2d> points = new List<Point2d>
                                        {
                                             acPolyline.GetPoint2dAt(0),
                                             acPolyline.GetPoint2dAt(1),
                                             acPolyline.GetPoint2dAt(2),
                                             acPolyline.GetPoint2dAt(3)
                                        };
                                        //根据y坐标进行排序
                                        points.Sort((a, b) => b.Y.CompareTo(a.Y));
                                        if (points[0].X < points[1].X)
                                        {
                                            polyline1.AddVertexAt(0, points[0], 0, 0, 0);
                                            polyline1.AddVertexAt(1, points[1], 0, 0, 0);
                                        }
                                        else
                                        {
                                            polyline1.AddVertexAt(0, points[1], 0, 0, 0);
                                            polyline1.AddVertexAt(1, points[0], 0, 0, 0);
                                        }
                                        if (points[2].X > points[3].X)
                                        {
                                            polyline1.AddVertexAt(2, points[2], 0, 0, 0);
                                            polyline1.AddVertexAt(3, points[3], 0, 0, 0);
                                        }
                                        else
                                        {
                                            polyline1.AddVertexAt(2, points[3], 0, 0, 0);
                                            polyline1.AddVertexAt(3, points[2], 0, 0, 0);
                                        }
                                        polyline1.Closed = true;
                                        //polyline1.NumberOfVertices = 4;
                                        Polylines.Add(polyline1);
                                        //File.AppendAllText("acPolyline.txt", polyline1.GetPoint2dAt(0) + " " + polyline1.GetPoint2dAt(2) + "\r\n");
                                        continue;
                                    }
                                    Polylines.Add(acPolyline);
                                }
                            }
                            //判断是否是块引用          
                            if (enttype == "AcDbBlockReference")
                            {
                                BlockReference blockRef = entity as BlockReference;

                                // 块引用的位置范围检查
                                if (blockRef.Position.X > leftx && blockRef.Position.X < rightx && blockRef.Position.Y < lefty && blockRef.Position.Y > righty)
                                {
                                    // 获取块的定义
                                    BlockTableRecord blockDef = blockRef.BlockTableRecord.GetObject(OpenMode.ForRead) as BlockTableRecord;
                                    blocks.Add(blockRef);
                                    //File.AppendAllText("blocks.txt", "Position: " + blockRef.Position + "Bounds: " + "maxpoint: " + blockRef.Bounds.Value.MaxPoint + "minpoint: " + blockRef.Bounds.Value.MinPoint + "\r\n");

                                }
                            }
                        }
                    }
                    #endregion
                    // 这里删除文件是不是应该先检查一下文件是否存在
                    File.Delete("x1.txt");
                    #region 点在线上函数和点到线段距离函数
                    bool GetPointIsInLine(Point3d pf, Point3d p1, Point3d p2, double range)
                    {
                        //range 判断的的误差，不需要误差则赋值0
                        //点在线段首尾两端之外则return false
                        double cross = (p2.X - p1.X) * (pf.X - p1.X) + (p2.Y - p1.Y) * (pf.Y - p1.Y);
                        if (cross <= 0) return false;
                        double d2 = (p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y);
                        if (cross >= d2) return false;
                        double r = cross / d2;
                        double px = p1.X + (p2.X - p1.X) * r;
                        double py = p1.Y + (p2.Y - p1.Y) * r;
                        //判断距离是否小于误差
                        return (pf.X - px) * (pf.X - px) + (py - pf.Y) * (py - pf.Y) <= range * range;
                    }
                    /*                    double DistancePointToLineSegment(Point3d point, Point3d lineStart, Point3d lineEnd)
                                        {
                                            // 如果起始点坐标比终点坐标大，交换起始点和终点
                                            if (lineEnd.X + 5 < lineStart.X || (Math.Abs(lineEnd.X - lineStart.X) < 1 && lineEnd.Y > lineStart.Y))
                                            {
                                                // 交换 lineStart 和 lineEnd
                                                Point3d temp = lineStart;
                                                lineStart = lineEnd;
                                                lineEnd = temp;
                                            }

                                            double dx = lineEnd.X - lineStart.X;
                                            double dy = lineEnd.Y - lineStart.Y;

                                            if ((dx == 0) && (dy == 0))
                                            {
                                                // 线段实际上是一个点
                                                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                                            }

                                            // 计算最小化距离的 t 值
                                            double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

                                            // 判断 t 是否在 [0, 1] 区间内
                                            if (t < 0)
                                            {
                                                return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                                            }
                                            else if (t > 1)
                                            {
                                                return Math.Sqrt(Math.Pow(point.X - lineEnd.X, 2) + Math.Pow(point.Y - lineEnd.Y, 2));
                                            }
                                            else
                                            {
                                                Point3d closest = new Point3d(lineStart.X + t * dx, lineStart.Y + t * dy, 0);
                                                return Math.Sqrt(Math.Pow(point.X - closest.X, 2) + Math.Pow(point.Y - closest.Y, 2));
                                            }
                                        }*/
                    double DistancePointToLineSegment(Point3d point, Point3d lineStart, Point3d lineEnd)
                    {
                        // 计算线段的方向向量
                        double dx = lineEnd.X - lineStart.X;
                        double dy = lineEnd.Y - lineStart.Y;

                        // 定义误差范围
                        double epsilon = 1.0;

                        // 如果是竖线（dx 接近 0），确保起点在终点上方
                        if (Math.Abs(dx) <= epsilon && lineStart.Y > lineEnd.Y)
                        {
                            // 交换起始点和终点
                            Point3d temp = lineStart;
                            lineStart = lineEnd;
                            lineEnd = temp;
                        }
                        // 如果是横线（dy 接近 0），确保起点在终点左边
                        else if (Math.Abs(dy) <= epsilon && lineStart.X > lineEnd.X)
                        {
                            // 交换起始点和终点
                            Point3d temp = lineStart;
                            lineStart = lineEnd;
                            lineEnd = temp;
                        }

                        // 重新计算方向向量
                        dx = lineEnd.X - lineStart.X;
                        dy = lineEnd.Y - lineStart.Y;

                        // 处理线段退化为点的情况
                        if ((dx == 0) && (dy == 0))
                        {
                            // 线段实际上是一个点
                            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                        }

                        // 计算最小化距离的 t 值
                        double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

                        // 判断 t 是否在 [0, 1] 区间内
                        if (t < 0)
                        {
                            // 投影点在起始点的“左边”或“下方”
                            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
                        }
                        else if (t > 1)
                        {
                            // 投影点在终点的“右边”或“上方”
                            return Math.Sqrt(Math.Pow(point.X - lineEnd.X, 2) + Math.Pow(point.Y - lineEnd.Y, 2));
                        }
                        else
                        {
                            // 投影点在线段上
                            Point3d closest = new Point3d(lineStart.X + t * dx, lineStart.Y + t * dy, 0);
                            return Math.Sqrt(Math.Pow(point.X - closest.X, 2) + Math.Pow(point.Y - closest.Y, 2));
                        }
                    }

                    #endregion

                    #region 确定初始线（用户指定）
                    double min_distance = 1000.0;
                    #endregion

                    #region 根据线长筛选所有主线
                    // 这里删除文件是不是应该先检查一下文件是否存在
                    //File.Delete("debug1.txt");
                    File.Delete("x.txt");
                    List<Line> f_line = new List<Line>();

                    foreach (var i in lines)
                    {
                        if (Math.Abs(i.StartPoint.X - Global.fline.StartPoint.X) < 10f && i.Length / Global.fline.Length < 1.1 && i.Length / Global.fline.Length > 0.8)
                        {
                            f_line.Add(i);
                        }
                    }
                    //File.AppendAllText("x.txt", f_line.Count + "\r\n");//多了盒子的两条 
                    #endregion

                    #region 找到主线上的所有分支点

                    List<Line> st_line = new List<Line>();
                    List<Line> et_line = new List<Line>();
                    List<my_line> nmyl = new List<my_line>();
                    foreach (var i in mylines)
                    {
                        nmyl.Add(i);
                    }

                    foreach (var ij in f_line)
                    {
                        foreach (var i in mylines)
                        {
                            if (GetPointIsInLine(i.line.StartPoint, ij.StartPoint, ij.EndPoint, 1.6))//去掉一个是0.22
                            {
                                st_line.Add(i.line);
                                nmyl.Remove(i);
                            }
                        }
                    }
                    /*File.Delete("st_line.txt");
                    foreach (var i in st_line)
                    {
                        File.AppendAllText("st_line.txt", "StartPoint:" + i.StartPoint.X + " " + i.StartPoint.Y + "EndPoint:" + i.EndPoint.X + " " + i.EndPoint.Y + "\r\n");
                    }

                    File.Delete("f_line.txt");
                    foreach (var i in f_line)
                    {
                        File.AppendAllText("f_line.txt", "StartPoint:" + i.StartPoint.X + " " + i.StartPoint.Y + "EndPoint:" + i.EndPoint.X + " " + i.EndPoint.Y + "\r\n");
                    }
                    File.Delete("mylines.txt");*/
                    mylines = nmyl;
                    #endregion


                    #region 计算线段中点
                    Point3d CalculateMidPoint(Point3d point1, Point3d point2)
                    {
                        double midX = (point1.X + point2.X) / 2;
                        double midY = (point1.Y + point2.Y) / 2;
                        double midZ = (point1.Z + point2.Z) / 2;

                        return new Point3d(midX, midY, midZ);
                    }
                    #endregion

                    // 这里筛选保险丝的逻辑是否正确
                    #region 筛选多段线得到保险丝（出入中点）及其他盒子
                    //用于存储中间多段线
                    List<Teigha.DatabaseServices.Polyline> midpl = new List<Teigha.DatabaseServices.Polyline>();
                    //用于存储特定条件的多段线
                    List<Teigha.DatabaseServices.Polyline> bx_poly = new List<Teigha.DatabaseServices.Polyline>();
                    //遍历 Polylines 列表，筛选出闭合的且顶点数为4的多段线
                    double fuseArea = 0;
                    double fuseDis = 20.0;
                    foreach (var i in Polylines)
                    {
                        if (i.Closed && i.NumberOfVertices == 4)
                        {
                            double dis = Math.Sqrt(Math.Pow(i.GetPoint2dAt(1).X - Global.fusePoint.X, 2) +
                                Math.Pow(i.GetPoint2dAt(1).Y - Global.fusePoint.Y, 2));
                            if (dis < fuseDis)
                            {
                                fuseArea = i.Area;
                                fuseDis = dis;
                            }
                        }
                    }
                    foreach (var i in Polylines)
                    {
                        //根据多段线的面积与32的差的绝对值是否小于0.5，将多段线分别添加到 bx_poly 或 midpl。
                        if (i.Closed && i.NumberOfVertices == 4)
                        {
                            if (fuseArea != 0 && Math.Abs(i.Area - fuseArea) < 0.5)
                            {
                                bx_poly.Add(i);
                            }
                            else
                            {
                                midpl.Add(i);
                            }
                        }

                    }
                    double width = 0;
                    /*                    File.Delete("midpl.txt");
                                        foreach (var i in midpl)
                                        {
                                            File.AppendAllText("midpl.txt", "StartPoint:" + i.StartPoint.X + " " + i.StartPoint.Y + "EndPoint:" + i.EndPoint.X + " " + i.EndPoint.Y + "\r\n");
                                        }*/
                    /*                    File.Delete("bx_poly.txt");
                                        foreach (var i in bx_poly)
                                        {
                                            File.AppendAllText("bx_poly.txt", "StartPoint:" + i.StartPoint.X + " " + i.StartPoint.Y + "EndPoint:" + i.EndPoint.X + " " + i.EndPoint.Y + "\r\n");
                                        }*/
                    // 保险丝文本识别
                    /*                    List<DBText> DbxText = new List<DBText>();
                                        List<MText> MbxText = new List<MText>();
                                        File.Delete("DbxText.txt");
                                        File.Delete("MbxText.txt");*/
                    File.Delete("bxtxt.txt");
                    for (int j = 0; j < txts.Count; j++)
                    {
                        if (txts[j].Height == Global.fuseTextHeight)
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(txts[j].TextString, txts[j].Position);
                            bxtxt.Add(newtext);
                            File.AppendAllText("bxtxt.txt", txts[j].TextString + txts[j].Position + "\r\n");
                        }
                    }
                    for (int j = 0; j < mtxts.Count; j++)
                    {
                        if (mtxts[j].TextHeight == Global.fuseTextHeight)
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(mtxts[j].Text, mtxts[j].Location);
                            bxtxt.Add(newtext);
                            File.AppendAllText("bxtxt.txt", mtxts[j].Text + mtxts[j].Location + "\r\n");
                        }
                    }
                    // 处理单行文本
                    /*                    if (Global.isDText)
                                        {
                                            // 先将全部保险丝单行文本保存起来，后面可以进行分组再合并
                                            for (int j = 0; j < txts.Count; j++)
                                            {
                                                if (txts[j].Height == Global.fuseTextHeight)
                                                {
                                                    DbxText.Add(txts[j]);
                                                    File.AppendAllText("DbxText.txt", txts[j].TextString + txts[j].Position +  "\r\n");
                                                }
                                            }
                                        }
                                        // 处理多行文本
                                        else
                                        {
                                            for (int j = 0; j < mtxts.Count; j++)
                                            {
                                                if(mtxts[j].TextHeight == Global.fuseTextHeight && mtxts[j].Text.Length > 4)
                                                {
                                                    MbxText.Add(mtxts[j]);
                                                    File.AppendAllText("MbxText.txt", mtxts[j].Text + mtxts[j].Location + "\r\n");
                                                }
                                            }
                                        }*/
                    // 如果是单行文本，将这些文本根据起点坐标进行分组
                    //File.Delete("GroupDbxText.txt");
                    //File.Delete("MergeDbxText.txt");
                    List<TextWithCoordinates> mergeDbxText = new List<TextWithCoordinates>();
                    int H = (int)(Global.fuseTextHeight + 4.5);
                    //File.AppendAllText("GroupDbxText.txt", H + "\r\n");
                    List<List<TextWithCoordinates>> groups = new List<List<TextWithCoordinates>>();

                    foreach (var item in bxtxt)
                    {
                        bool addedToGroup = false;
                        foreach (var group in groups)
                        {
                            if (group.Any())
                            {
                                var firstItemInGroup = group.First();
                                if (Math.Abs(firstItemInGroup.Coordinates.X - item.Coordinates.X) <= 1 &&
                                    Math.Abs(firstItemInGroup.Coordinates.Y - item.Coordinates.Y) <= H)
                                {
                                    group.Add(item);
                                    addedToGroup = true;
                                    break;
                                }
                            }
                        }

                        if (!addedToGroup)
                        {
                            groups.Add(new List<TextWithCoordinates> { item });
                        }
                    }
                    for (int j = 0; j < groups.Count; j++)
                    {
                        if (groups[j].Count == 2)
                        {
                            TextWithCoordinates mertext = new TextWithCoordinates();
                            if (groups[j][0].Coordinates.Y > groups[j][1].Coordinates.Y)
                            {
                                mertext.Coordinates = new Point3d(groups[j][0].Coordinates.X, groups[j][0].Coordinates.Y, 0.0);
                                mertext.Text = groups[j][0].Text + " " + groups[j][1].Text;
                            }
                            else
                            {
                                mertext.Coordinates = new Point3d(groups[j][1].Coordinates.X, groups[j][1].Coordinates.Y, 0.0);
                                mertext.Text = groups[j][1].Text + " " + groups[j][0].Text;
                            }
                            //File.AppendAllText("MergeDbxText.txt", "合并后的文本：" + mertext.Text + "坐标：" + mertext.Coordinates + "\r\n");
                        }
                    }
                    //用于存储包含多段线和文本对象的自定义类型
                    //File.Delete("bp.txt");
                    //File.Delete("point_bp.txt");
                    List<bx_pl> bp = new List<bx_pl>();
                    List<TextWithCoordinates> included_bx = new List<TextWithCoordinates>();
                    //遍历 bx_poly 列表
                    foreach (var i in bx_poly)
                    {
                        double fuseDistance = double.PositiveInfinity;
                        //对于每个多段线，创建 bx_pl 对象，并将多段线赋值给 pl 属性
                        bx_pl midbp;
                        midbp.pl = i;
                        midbp.bxtext = new TextWithCoordinates();
                        //计算宽度
                        //width = Math.Abs(i.GetPoint2dAt(0).X - i.GetPoint2dAt(1).X);
                        //计算多段线中间两个顶点的中点作为 top 属性，以及首尾两个顶点的中点作为 down 属性
                        midbp.top = new Point3d((i.GetPoint2dAt(0).X + i.GetPoint2dAt(1).X) / 2, (i.GetPoint2dAt(0).Y + i.GetPoint2dAt(1).Y) / 2, 0);
                        midbp.down = new Point3d((i.GetPoint2dAt(2).X + i.GetPoint2dAt(3).X) / 2, (i.GetPoint2dAt(2).Y + i.GetPoint2dAt(3).Y) / 2, 0);//直接取最后俩点
                        //File.AppendAllText("point_bp.txt","top: " + midbp.top + " down: " + midbp.down + "\r\n");
                        Point2d ploy = new Point2d(midbp.top.X, (midbp.top.Y + midbp.down.Y) / 2);
                        TextWithCoordinates curText = new TextWithCoordinates();
                        //遍历 mtxts 列表，这个列表可能包含文本对象
                        //File.AppendAllText("bp.txt", mtxts[index].Text + midbp.top.X + " " + midbp.down.X + "\r\n");
                        for (int j = 0; j < groups.Count; j++)
                        {
                            if (groups[j].Count == 2)
                            {
                                TextWithCoordinates mertext = new TextWithCoordinates();
                                if (groups[j][0].Coordinates.Y > groups[j][1].Coordinates.Y)
                                {
                                    mertext.Coordinates = new Point3d(groups[j][0].Coordinates.X, groups[j][0].Coordinates.Y, 0.0);
                                    mertext.Text = groups[j][0].Text + " " + groups[j][1].Text;
                                }
                                else
                                {
                                    mertext.Coordinates = new Point3d(groups[j][1].Coordinates.X, groups[j][1].Coordinates.Y, 0.0);
                                    mertext.Text = groups[j][1].Text + " " + groups[j][0].Text;
                                }
                                //File.AppendAllText("MergeDbxText.txt", "合并后的文本：" + mertext.Text + "坐标：" + mertext.Coordinates + "\r\n");
                                double dis = Math.Sqrt(Math.Pow(mertext.Coordinates.X - ploy.X, 2) + Math.Pow(mertext.Coordinates.Y - ploy.Y, 2));
                                if (dis < fuseDistance)
                                {
                                    fuseDistance = dis;
                                    curText = mertext;
                                }
                            }
                            else if (groups[j].Count == 1)
                            {
                                double dis = Math.Sqrt(Math.Pow(groups[j][0].Coordinates.X - ploy.X, 2) + Math.Pow(groups[j][0].Coordinates.Y - ploy.Y, 2));
                                if (dis < fuseDistance)
                                {
                                    fuseDistance = dis;
                                    curText = groups[j][0];
                                }
                            }
                        }
                        included_bx.Add(curText);
                        midbp.bxtext = curText;
                        bp.Add(midbp);

                    }

                    #endregion
                    #region 复合实体保险丝
                    //File.Delete("blockbp.txt");
                    if (bp.Count() > 0)
                    {
                        double areaBp = bp.Last<bx_pl>().pl.Area;
                        foreach (var i in blocks)
                        {
                            double fuseDistance = double.PositiveInfinity;
                            bx_pl midbp;
                            midbp.bxtext = new TextWithCoordinates();
                            Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();
                            double lengthBlocks = i.Bounds.Value.MaxPoint.X - i.Bounds.Value.MinPoint.X;
                            double widthBlocks = i.Bounds.Value.MaxPoint.Y - i.Bounds.Value.MinPoint.Y;
                            double area = lengthBlocks * widthBlocks;
                            double disthread = Math.Sqrt(Math.Pow(lengthBlocks / 2, 2) + Math.Pow(widthBlocks / 2, 2)) + 5;
                            //File.AppendAllText("blockbp.txt", area + "\r\n");
                            if (Math.Abs(area - areaBp) < 30)
                            {
                                polyline1.AddVertexAt(0, new Point2d(i.Bounds.Value.MinPoint.X, i.Bounds.Value.MaxPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(1, new Point2d(i.Bounds.Value.MaxPoint.X, i.Bounds.Value.MaxPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(2, new Point2d(i.Bounds.Value.MaxPoint.X, i.Bounds.Value.MinPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(3, new Point2d(i.Bounds.Value.MinPoint.X, i.Bounds.Value.MinPoint.Y), 0, 0, 0);
                                polyline1.Closed = true;
                                midbp.pl = polyline1;
                                midbp.top = new Point3d((polyline1.GetPoint2dAt(0).X + polyline1.GetPoint2dAt(1).X) / 2, polyline1.GetPoint2dAt(0).Y, 0);
                                midbp.down = new Point3d((polyline1.GetPoint2dAt(3).X + polyline1.GetPoint2dAt(2).X) / 2, polyline1.GetPoint2dAt(2).Y, 0);//直接取最后俩点A
                                Point2d ploy = new Point2d(midbp.top.X, (midbp.top.Y + midbp.down.Y) / 2);
                                TextWithCoordinates curText = new TextWithCoordinates();
                                for (int j = 0; j < groups.Count; j++)
                                {
                                    if (groups[j].Count == 2)
                                    {
                                        TextWithCoordinates mertext = new TextWithCoordinates();
                                        if (groups[j][0].Coordinates.Y > groups[j][1].Coordinates.Y)
                                        {
                                            mertext.Text = groups[j][0].Text + " " + groups[j][1].Text;
                                            mertext.Coordinates = new Point3d(groups[j][0].Coordinates.X, groups[j][0].Coordinates.Y, 0.0);
                                        }
                                        else
                                        {
                                            mertext.Text = groups[j][1].Text + " " + groups[j][0].Text;
                                            mertext.Coordinates = new Point3d(groups[j][1].Coordinates.X, groups[j][1].Coordinates.Y, 0.0);
                                        }
                                        double dis = Math.Sqrt(Math.Pow(mertext.Coordinates.X - ploy.X, 2) + Math.Pow(mertext.Coordinates.Y - ploy.Y, 2));
                                        if (dis < fuseDistance)
                                        {
                                            fuseDistance = dis;
                                            curText = mertext;
                                        }
                                    }
                                    else if (groups[j].Count == 1)
                                    {
                                        double dis = Math.Sqrt(Math.Pow(groups[j][0].Coordinates.X - ploy.X, 2) + Math.Pow(groups[j][0].Coordinates.Y - ploy.Y, 2));
                                        if (dis < fuseDistance)
                                        {
                                            fuseDistance = dis;
                                            curText = groups[j][0];
                                        }
                                    }
                                }
                                if (fuseDistance < disthread)
                                {
                                    included_bx.Add(curText);
                                    midbp.bxtext = curText;
                                    bp.Add(midbp);
                                }
                            }
                        }
                    }
                    #endregion

                    #region circle保险丝
                    // 分组逻辑
                    List<List<Circle>> groupedCircles = new List<List<Circle>>();

                    foreach (var circle in cirs)
                    {
                        bool addedToGroup = false;
                        foreach (var group in groupedCircles)
                        {
                            // 检查是否满足分组条件
                            if (group.Any())
                            {
                                var firstCircleInGroup = group.First();
                                if (Math.Abs(circle.Radius - firstCircleInGroup.Radius) < 1 &&
                                    Math.Abs(circle.Center.X - firstCircleInGroup.Center.X) < 1 &&
                                    Math.Abs(circle.Center.Y - firstCircleInGroup.Center.Y) < 20)
                                {
                                    group.Add(circle);
                                    addedToGroup = true;
                                    break;
                                }
                            }
                        }

                        if (!addedToGroup)
                        {
                            groupedCircles.Add(new List<Circle> { circle });
                        }
                    }
                    //存储处理后的圆形对象
                    List<bx_cir> new_bp = new List<bx_cir>();
                    //遍历groupedCircles中的每个分组
                    //File.Delete("new_bp.txt");
                    //File.Delete("group.txt");
                    //File.Delete("circle未匹配");
                    foreach (var group in groupedCircles)
                    {
                        //获取当前分组中的圆形数量，并存储在变量tt中
                        int tt = group.Count();
                        //如果分组中的圆形数量为2，则执行以下代码块
                        if (tt == 2)
                        {
                            bool flag = false;
                            //File.AppendAllText("group.txt", $"  Circle: Radius={group.First().Radius}, Center=({group.First().Center.X}, {group.First().Center.Y}) \r\n");
                            //File.AppendAllText("group.txt", $"  Circle: Radius={group.ElementAtOrDefault(1).Radius}, Center=({group.ElementAtOrDefault(1).Center.X}, {group.ElementAtOrDefault(1).Center.Y}) \r\n");
                            //File.AppendAllText("group.txt", "\r\n");
                            bx_cir midbp;
                            midbp.cirText = new TextWithCoordinates();
                            //分组中的第一个圆形赋值给midbp.top属性
                            if (group.First().Center.Y > group.ElementAtOrDefault(1).Center.Y)
                            {
                                midbp.top = group.First();
                                midbp.down = group.ElementAtOrDefault(1);
                            }
                            else
                            {
                                midbp.top = group.ElementAtOrDefault(1);
                                midbp.down = group.First();
                            }
                            for (int j = 0; j < groups.Count; j++)
                            {
                                if (groups[j].Count == 2)
                                {
                                    TextWithCoordinates mertext = new TextWithCoordinates();
                                    if (groups[j][0].Coordinates.Y > groups[j][1].Coordinates.Y)
                                    {
                                        mertext.Text = groups[j][0].Text + " " + groups[j][1].Text;
                                        mertext.Coordinates = new Point3d(groups[j][0].Coordinates.X, groups[j][0].Coordinates.Y, 0.0);
                                    }
                                    else
                                    {
                                        mertext.Text = groups[j][1].Text + " " + groups[j][0].Text;
                                        mertext.Coordinates = new Point3d(groups[j][1].Coordinates.X, groups[j][1].Coordinates.Y, 0.0);
                                    }
                                    if (DistancePointToLineSegment(mertext.Coordinates, group.First().Center, group.ElementAtOrDefault(1).Center) < 20f && !included_bx.Contains(mertext))
                                    {
                                        //将文本对象mtxts[j]赋值给midbp.txt
                                        midbp.cirText = mertext;
                                        //使用正则表达式检查文本是否以字母开头
                                        if (Regex.IsMatch(midbp.cirText.Text, @"^[\p{L}]"))
                                        {
                                            //将midbp添加到new_bp列表中
                                            new_bp.Add(midbp);
                                            included_bx.Add(midbp.cirText);
                                            //File.AppendAllText("new_bp.txt", mertext.Text + " " + group.First().Center.X + " " + group.First().Center.Y + " " + group.ElementAtOrDefault(1).Center.X + " " + group.ElementAtOrDefault(1).Center.Y + "\r\n");
                                        }
                                        break;
                                    }
                                }
                                else if (groups[j].Count == 1)
                                {
                                    if (DistancePointToLineSegment(groups[j][0].Coordinates, group.First().Center, group.ElementAtOrDefault(1).Center) < 12f && !included_bx.Contains(groups[j][0]))
                                    {
                                        //将文本对象mtxts[j]赋值给midbp.txt
                                        midbp.cirText = groups[j][0];
                                        //使用正则表达式检查文本是否以字母开头
                                        if (Regex.IsMatch(midbp.cirText.Text, @"^[\p{L}]"))
                                        {
                                            //将midbp添加到new_bp列表中
                                            new_bp.Add(midbp);
                                            included_bx.Add(midbp.cirText);
                                            //File.AppendAllText("new_bp.txt", midbp.cirText.Text + " " + group.First().Center.X + " " + group.First().Center.Y + " " + group.ElementAtOrDefault(1).Center.X + " " + group.ElementAtOrDefault(1).Center.Y + "\r\n");
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                    #region 筛选引脚，颜色为跟随图层（如果输入实体，可能不需要这样）
                    List<TextWithCoordinates> yjtxt = new List<TextWithCoordinates>();
                    List<TextWithCoordinates> qjtxt = new List<TextWithCoordinates>();
                    //File.Delete("qjtxt.txt");
                    //File.Delete("yjtxt.txt");
                    //File.Delete("height.txt");
                    for (int j = 0; j < txts.Count; j++)
                    {
                        //File.AppendAllText("height.txt", txts[j].Height + "\r\n");
                        if (txts[j].Height == Global.dk_height && !txts[j].TextString.Contains('.'))
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(txts[j].TextString, txts[j].Position);
                            yjtxt.Add(newtext);
                            //File.AppendAllText("yjtxt.txt", txts[j].TextString + " 坐标： " + txts[j].Position.X + " " + txts[j].Position.Y + "\r\n");
                        }
                        else if (txts[j].TextString.Any(c => !char.IsDigit(c)) && !txts[j].TextString.Contains('#') && !txts[j].TextString.Contains('.') && (Char.IsLetter(txts[j].TextString[0]) ||
                            Char.IsDigit(txts[j].TextString[0]) && Math.Abs(txts[j].Height - Global.qj_height) < 0.5) && Math.Abs(txts[j].Height - Global.qj_height) < 0.5 || (txts[j].TextString[0] >= 0x4E00 && txts[j].TextString[0] <= 0x9FA5 && Math.Abs(txts[j].Height - Global.qj_height) < 0.5 && !txts[j].TextString.Contains('.')))
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(txts[j].TextString, txts[j].Position);
                            qjtxt.Add(newtext);
                            //File.AppendAllText("qjtxt.txt", txts[j].TextString + " " + txts[j].Position.X + " " + txts[j].Position.Y + " " + Global.qj_height + "\r\n");
                        }
                    }
                    for (int j = 0; j < mtxts.Count; j++)
                    {
                        //File.AppendAllText("height.txt", mtxts[j].TextHeight + "\r\n");
                        if (mtxts[j].TextHeight == Global.dk_height && !mtxts[j].Text.Contains('.'))
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(mtxts[j].Text, mtxts[j].Location);
                            yjtxt.Add(newtext);
                            //File.AppendAllText("yjtxt.txt", mtxts[j].Text + " 坐标： " + mtxts[j].Location.X + " " + mtxts[j].Location.Y + "\r\n");
                        }
                        else if (mtxts[j].Text.Any(c => !char.IsDigit(c)) && !mtxts[j].Text.Contains('#') && (Char.IsLetter(mtxts[j].Text[0]) || Char.IsDigit(mtxts[j].Text[0]) && mtxts[j].TextHeight == Global.qj_height) && (mtxts[j].TextHeight == Global.qj_height
                            || mtxts[j].TextHeight + 1.5 == Global.qj_height) || (mtxts[j].Text[0] >= 0x4E00 && mtxts[j].Text[0] <= 0x9FA5 && mtxts[j].TextHeight == Global.qj_height))
                        {
                            TextWithCoordinates newtext = new TextWithCoordinates(mtxts[j].Text, mtxts[j].Location);
                            qjtxt.Add(newtext);
                            //File.AppendAllText("qjtxt.txt", mtxts[j].Text + " " + mtxts[j].Location.X + " " + mtxts[j].Location.Y + " " + Global.qj_height + "\r\n");
                        }
                    }
                    #endregion

                    #region 点在矩形poly内
                    bool IsPointInsideRectangle(Point3d point, Teigha.DatabaseServices.Polyline polyline)
                    {
                        if (!polyline.Closed || polyline.NumberOfVertices != 4)
                        {
                            return false; // 如果polyline不是闭合的或不是矩形，则返回false
                        }

                        Point2d topLeft = polyline.GetPoint2dAt(0);
                        Point2d topRight = polyline.GetPoint2dAt(1);
                        Point2d bottomRight = polyline.GetPoint2dAt(2);
                        Point2d bottomLeft = polyline.GetPoint2dAt(3);
                        return point.X >= topLeft.X && point.X <= bottomRight.X && point.Y >= bottomRight.Y && point.Y <= topLeft.Y;
                    }
                    #endregion



                    #region 矩形负载元器件识别
                    //qjl 用于存储 Q_j 对象
                    List<Q_j> qjl = new List<Q_j>();
                    /*                    List<Teigha.DatabaseServices.Polyline> temp = new List<Teigha.DatabaseServices.Polyline>();*/
                    //no_qj 用于存储未被分配到任何线段或多段线的 MText 对象
                    List<TextWithCoordinates> no_qj = new List<TextWithCoordinates>();
                    double battery_point_X = 0.0;
                    double battery_point_Y = 0.0;
                    foreach (var i in midpl)
                    {
                        foreach (var ij in qjtxt)
                        {
                            //使用 IsPointInsideRectangle 函数检查文本对象的坐标是否在中线的矩形区域内
                            if (IsPointInsideRectangle(ij.Coordinates, i)/*|| DistancePointToLineSegment(ij.Location, i.GetPoint3dAt(2), i.GetPoint3dAt(3)) <10f*/)
                            {
                                //创建一个 Q_j 对象，将文本对象和中线关联起来，然后添加到 qjl 和 no_qj 列表
                                Q_j q;
                                q.txt = ij;
                                q.pl = i;
                                qjl.Add(q);
                                no_qj.Add(ij);
                            }
                            //对蓄电池进行处理
                            else if (!(IsPointInsideRectangle(ij.Coordinates, i)) && !no_qj.Contains(ij) && ij.Text != "12V蓄电池" && ij.Text.Contains("蓄电池"))
                            {
                                if (Math.Abs(ij.Coordinates.X - (i.StartPoint.X + i.EndPoint.X) / 2.0) <= 10f && (i.Closed || i.NumberOfVertices == 4))
                                {
                                    Q_j q;
                                    q.txt = ij;
                                    q.pl = i;
                                    qjl.Add(q);
                                    no_qj.Add(ij);
                                    battery_point_X = ij.Coordinates.X;
                                    battery_point_Y = ij.Coordinates.Y;
                                }
                            }

                        }
                    }
                    /*                    File.Delete("no_qj.txt");
                                        foreach (var i in no_qj)
                                        {
                                            File.AppendAllText("no_qj.txt", i.Text + i.Coordinates.X + " " + i.Coordinates.Y + "\r\n");
                                        }*/
                    //用于存储未被分配的文本对象与最近线段的关联。
                    List<mqj> mqj1 = new List<mqj>();
                    //给接插件名找边
                    //循环遍历 qjtxt，找出未被添加到 no_qj 的文本对象
                    foreach (var ij in qjtxt)
                    {
                        double minDistance = double.MaxValue;
                        if (!no_qj.Contains(ij))
                        {
                            Line closestLine = new Line();
                            foreach (var i in mylines)
                            {
                                if (Math.Abs(i.line.StartPoint.Y - i.line.EndPoint.Y) < 0.5f && i.line.Length < Global.fline.Length - 20
                                   /*&& ij.Location.X > i.line.StartPoint.X && ij.Location.X + (ij.Text.Length - 3)*ij.TextHeight < i.line.EndPoint.X*/)
                                {
                                    //对于每个文本对象，寻找最近的线段，使用 DistancePointToLineSegment 函数计算距离。
                                    double distance = DistancePointToLineSegment(ij.Coordinates, i.line.StartPoint, i.line.EndPoint); // 计算Line与指定点的距离

                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestLine = i.line;
                                    }
                                }

                            }
                            //如果找到距离最近的线段，创建一个 mqj 对象，将文本对象和线段关联起来，然后添加到 mqj1 列表。
                            if (minDistance < 30)
                            {
                                mqj m1;
                                m1.line = closestLine;
                                m1.txt = ij;
                                mqj1.Add(m1);
                            }
                        }
                    }
                    /*                    File.Delete("mqj1.txt");
                                        foreach (var i in mqj1)
                                        {

                                            File.AppendAllText("mqj1.txt", "模块名称：" + i.txt.Text + "StartPoint:" + i.line.StartPoint + " EndPoint: " + i.line.EndPoint + "\r\n");
                                        }*/
                    //循环遍历 mqj1 列表，对于每个 mqj 对象
                    //File.Delete("points_line.txt");
                    List<TextWithCoordinates> finished = new List<TextWithCoordinates>();
                    foreach (var ij in mqj1)
                    {
                        bool polyflag = false;
                        //再次循环遍历 mylines（可能是线段列表），寻找与 mqj 中线段长度相近且端点坐标接近的线段。
                        foreach (var i in mylines)
                        {
                            if (Math.Abs(i.line.StartPoint.X - ij.line.StartPoint.X) < 1f && Math.Abs(i.line.EndPoint.X - ij.line.EndPoint.X) < 1f && Math.Abs(i.line.StartPoint.Y - ij.line.StartPoint.Y) > 10)
                            {
                                var line1 = i.line;
                                var line2 = ij.line;
                                //将两个线段的端点添加到多段线中，并将其封闭
                                Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();

                                // 添加顶点
                                if (line1.StartPoint.Y > line2.StartPoint.Y && line1.StartPoint.Y > ij.txt.Coordinates.Y && line2.StartPoint.Y < ij.txt.Coordinates.Y)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyflag = true;
                                }
                                else if (line1.StartPoint.Y < line2.StartPoint.Y && line2.StartPoint.Y > ij.txt.Coordinates.Y && line1.StartPoint.Y < ij.txt.Coordinates.Y)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyflag = true;
                                }
                                // File.AppendAllText("points_line.txt", line1.StartPoint + "  " + line1.EndPoint + "\r\n");
                                // File.AppendAllText("points_line.txt", line2.StartPoint + "  " + line2.EndPoint + "\r\n");
                                //创建一个 Q_j 对象，将文本对象和多段线关联起来，然后添加到 qjl 列表
                                if (polyflag)
                                {
                                    polyline1.Closed = true;
                                    Q_j q;
                                    q.txt = ij.txt;
                                    q.pl = polyline1;
                                    qjl.Add(q);
                                    polyflag = false;
                                    finished.Add(ij.txt);
                                }
                            }
                        }
                    }

                    #region 对构建不成功的使用竖线进行构建
                    List<mqj> unfinished = new List<mqj>();
                    foreach (var ij in qjtxt)
                    {
                        double minDistance = double.MaxValue;
                        if (!finished.Contains(ij))
                        {
                            bool flag0 = false;
                            my_line closestLine = new my_line();
                            foreach (var i in mylines)
                            {
                                if (Math.Abs(i.line.StartPoint.X - i.line.EndPoint.X) < 0.5f && i.line.Length < Global.fline.Length - 30
                                   && ij.Coordinates.Y < i.line.StartPoint.Y && ij.Coordinates.Y > i.line.EndPoint.Y)
                                {
                                    //对于每个文本对象，寻找最近的线段，使用 DistancePointToLineSegment 函数计算距离。
                                    double distance = DistancePointToLineSegment(ij.Coordinates, i.line.StartPoint, i.line.EndPoint); // 计算Line与指定点的距离

                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestLine = i;
                                        flag0 = true;
                                    }
                                }

                            }
                            //如果找到距离最近的线段，创建一个 mqj 对象，将文本对象和线段关联起来，然后添加到 mqj1 列表。
                            if (flag0)
                            {
                                mqj m1;
                                m1.line = closestLine.line;
                                m1.txt = ij;
                                unfinished.Add(m1);
                            }
                        }
                    }
                    foreach (var ij in unfinished)
                    {
                        bool polyflag = false;
                        //再次循环遍历 mylines（可能是线段列表），寻找与 mqj 中线段长度相近且端点坐标接近的线段。
                        foreach (var i in mylines)
                        {
                            if (Math.Abs(i.line.StartPoint.Y - ij.line.StartPoint.Y) < 0.5f && Math.Abs(i.line.EndPoint.Y - ij.line.EndPoint.Y) < 0.5f && Math.Abs(i.line.StartPoint.X - ij.line.StartPoint.X) > 5)
                            {
                                var line1 = i.line;
                                var line2 = ij.line;
                                //将两个线段的端点添加到多段线中，并将其封闭
                                Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();

                                // 添加顶点
                                if (line1.StartPoint.X > line2.StartPoint.X && line1.StartPoint.X > ij.txt.Coordinates.X && line2.StartPoint.X < ij.txt.Coordinates.X)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    polyflag = true;
                                }
                                else if (line1.StartPoint.X < line2.StartPoint.X && line2.StartPoint.X > ij.txt.Coordinates.X && line1.StartPoint.X < ij.txt.Coordinates.X)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    polyflag = true;
                                }
                                if (polyflag)
                                {
                                    polyline1.Closed = true;
                                    Q_j q;
                                    q.txt = ij.txt;
                                    q.pl = polyline1;
                                    qjl.Add(q);
                                    polyflag = false;
                                }
                            }
                        }
                    }
                    #endregion
                    /*                    File.Delete("qjl.txt");
                                        foreach (var i in qjl)
                                        {
                                            File.AppendAllText("qjl.txt", i.txt.Text + i.pl.GetPoint2dAt(0) + " " + i.pl.GetPoint2dAt(2) + "\r\n");
                                        }*/
                    //初始化 myqj 列表，用于存储最终的关联对象，包括文本对象、线段和附加的文本对象
                    List<Myqj> myqj = new List<Myqj>();
                    //循环遍历 qjl 列表，对于每个 Q_j 对象
                    foreach (var i in qjl)
                    {
                        //创建一个 Myqj 对象，将附加的文本对象添加到 Myqj 对象的 yj 列表中
                        Myqj q1;
                        q1.yj = new List<TextWithCoordinates>();
                        foreach (var ij in yjtxt)
                        {
                            if (IsPointInsideRectangle(ij.Coordinates, i.pl))
                                q1.yj.Add(ij);
                        }
                        //如果 yj 列表不为空，将 Myqj 对象添加到 myqj 列表
                        if (q1.yj.Count != 0)
                        {
                            q1.pl = i.pl;
                            q1.txt = i.txt;
                            myqj.Add(q1);
                        }
                    }
                    /*                    File.Delete("负载引脚.txt");
                                        foreach (var i in myqj)
                                        {
                                            File.AppendAllText("负载引脚.txt", "模块名称：" + i.txt.Text + "\r\n");
                                            foreach (var j in i.yj)
                                            {
                                                File.AppendAllText("负载引脚.txt", j.Text + "\r\n");
                                            }
                                        }*/
                    #endregion

                    #region 继电器识别
                    //File.Delete("jd.txt");
                    List<TextWithCoordinates> jd_txt = new List<TextWithCoordinates>();

                    // 分离出后缀文本（包含“继电器”）和前缀文本（不包含“继电器”）
                    List<TextWithCoordinates> relayTexts = new List<TextWithCoordinates>();
                    List<TextWithCoordinates> prefixTexts = new List<TextWithCoordinates>();

                    // 用来存储“继”和“电器”文本对象，检查是否需要合并
                    List<TextWithCoordinates> jiTexts = new List<TextWithCoordinates>();  // 存储包含“继”的文本
                    List<TextWithCoordinates> dianqiTexts = new List<TextWithCoordinates>();  // 存储包含“电器”的文本

                    for (int j = 0; j < alltxts.Count; j++)
                    {
                        var text = alltxts[j].Text;
                        var location = alltxts[j].Coordinates;

                        if (text.Contains("继") && !text.Contains("电器"))  // 只包含“继”的文本
                        {
                            jiTexts.Add(new TextWithCoordinates(text, location));
                        }
                        else if (text.Contains("电器") && !text.Contains("继"))  // 只包含“电器”的文本
                        {
                            dianqiTexts.Add(new TextWithCoordinates(text, location));
                        }
                        else if (text.Length >= 3 && text.Substring(text.Length - 3) == "继电器")
                        {
                            relayTexts.Add(new TextWithCoordinates(text, location));  // 包含“继电器”的文本作为后缀文本
                        }
                        else
                        {
                            prefixTexts.Add(new TextWithCoordinates(text, location));  // 没有“继电器”的文本作为前缀文本
                        }
                    }

                    // 设置合并阈值（坐标差值阈值）
                    double thresholdX = 2.7;
                    double thresholdY = Global.jd_height + 2.5;

                    //bool[] usedRelay = new bool[relayTexts.Count];  // 标记后缀文本是否被合并

                    // 合并“继”和“电器”文本为完整的“继电器”
                    for (int i = 0; i < jiTexts.Count; i++)
                    {
                        var jiText = jiTexts[i];

                        // 检查是否存在“电器”文本与其接近
                        foreach (var dianqiText in dianqiTexts)
                        {
                            // 如果“继”与“电器”在位置上足够接近，认为它们属于同一个“继电器”文本
                            if (Math.Abs(jiText.Coordinates.X - dianqiText.Coordinates.X) <= thresholdX &&
                                Math.Abs(jiText.Coordinates.Y - dianqiText.Coordinates.Y) <= thresholdY)
                            {
                                string mergedText = jiText.Text + dianqiText.Text;  // 合并“继”和“电器”
                                Point3d mergedLocation = new Point3d((jiText.Coordinates.X + dianqiText.Coordinates.X) / 2,
                                                                     (jiText.Coordinates.Y + dianqiText.Coordinates.Y) / 2, 0);  // 使用中点作为合并后的坐标

                                relayTexts.Add(new TextWithCoordinates(mergedText, mergedLocation));  // 将合并后的文本加入 relayTexts
                                dianqiTexts.Remove(dianqiText);  // 删除已合并的“电器”文本
                                break;  // 只合并一次
                            }
                        }
                    }

                    // 合并前缀和后缀文本，前缀文本与后缀文本合并，最终构成完整的“继电器”文本
                    foreach (var prefix in prefixTexts)
                    {
                        bool merged = false;

                        // 遍历所有后缀文本，判断前缀文本是否应该与后缀文本合并
                        for (int k = 0; k < relayTexts.Count; k++)
                        {
                            var relay = relayTexts[k];

                            if (Math.Abs(prefix.Coordinates.X - relay.Coordinates.X) <= thresholdX &&
                                Math.Abs(prefix.Coordinates.Y - relay.Coordinates.Y) <= thresholdY)
                            {
                                // 合并前缀文本与后缀文本
                                string mergedText = prefix.Text + relay.Text;
                                Point3d mergedLocation = new Point3d(prefix.Coordinates.X, prefix.Coordinates.Y, 0);

                                jd_txt.Add(new TextWithCoordinates(mergedText, mergedLocation));  // 添加合并后的文本
                                relayTexts.RemoveAt(k);  // 删除已合并的后缀文本
                                merged = true;
                                break;
                            }
                        }

                        // 如果没有合并，表示该前缀文本没有找到匹配的后缀文本，可以选择不加入jd_txt（已删除单独加入前缀文本的部分）
                    }

                    // 把未被合并的后缀文本（完整的“继电器”文本）加入 jd_txt
                    for (int k = 0; k < relayTexts.Count; k++)
                    {
                        jd_txt.Add(relayTexts[k]);  // 添加完整的继电器文本
                    }

                    // 在这里将 jd_txt 写入文件 jdqlbd.txt
                    File.Delete("jdqxbd2.txt");  // 先删除旧文件（如果存在）
                    foreach (var text in jd_txt)
                    {
                        File.AppendAllText("jdqxbd2.txt", $"{text.Text} Location: X={text.Coordinates.X}, Y={text.Coordinates.Y}\r\n");
                    }

                    //初始化 mqj2 列表，用于存储与继电器文本关联的最近线段
                    List<mqj> mqj2 = new List<mqj>();
                    //给继电器找边（要求格式）
                    //循环遍历 jd_txt 列表。
                    File.Delete("mqj2.txt");
                    foreach (var ij in jd_txt)
                    {

                        Line closestLine = null;
                        double minDistance = double.MaxValue;
                        foreach (var i in mylines)
                        {
                            //找出与其距离最近的垂直线段
                            if (Math.Abs(i.line.StartPoint.X - i.line.EndPoint.X) < 0.5f)
                            {
                                double distance = DistancePointToLineSegment(ij.Coordinates, i.line.StartPoint, i.line.EndPoint); // 计算Line与指定点的距离

                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    closestLine = i.line;
                                }
                            }

                        }
                        //创建一个 mqj 对象，将文本对象和线段关联起来，然后添加到 mqj2 列表。
                        if (minDistance < 5)
                        {
                            mqj m1;
                            m1.line = closestLine;
                            m1.txt = ij;
                            mqj2.Add(m1);
                            //File.AppendAllText("mqj2.txt", ij.Text + " " + "StartPoint: " + "X " + closestLine.StartPoint.X + " Y " + closestLine.StartPoint.Y + " EndPoint: " + "X " + closestLine.EndPoint.X + " Y " + closestLine.EndPoint.Y + "\r\n");
                        }
                    }
                    //初始化 qj2 列表，用于存储与继电器文本关联的多段线
                    List<Q_j> qj2 = new List<Q_j>();
                    //循环遍历 mqj2 列表
                    //File.Delete("jdPoints_line.txt");
                    //File.Delete("jdTest.txt");
                    // File.Delete("qj2.txt");
                    List<TextWithCoordinates> usedRelay = new List<TextWithCoordinates>();
                    foreach (var ij in mqj2)
                    {
                        bool isOk = false;
                        foreach (var i in mylines)
                        {
                            if (Math.Abs(i.line.StartPoint.X - i.line.EndPoint.X) < 0.5f && Math.Abs(ij.line.StartPoint.Y - i.line.StartPoint.Y) < 1f && Math.Abs(ij.line.EndPoint.Y - i.line.EndPoint.Y) < 1f
                                && Math.Abs(i.line.StartPoint.X - ij.line.StartPoint.X) < 40)
                            {
                                //如果找到符合条件的线段，创建一个多段线 polyline1，将两个线段的端点添加到多段线中，并将其封闭。
                                var line1 = i.line;
                                var line2 = ij.line;
                                Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();
                                if (line1.StartPoint.X < line2.StartPoint.X && ij.txt.Coordinates.X > line2.StartPoint.X)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    isOk = true;
                                }
                                else if (line1.StartPoint.X > line2.StartPoint.X && ij.txt.Coordinates.X < line2.StartPoint.X)
                                {
                                    polyline1.AddVertexAt(0, new Point2d(line2.StartPoint.X, line2.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(1, new Point2d(line1.StartPoint.X, line1.StartPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(2, new Point2d(line1.EndPoint.X, line1.EndPoint.Y), 0, 0, 0);
                                    polyline1.AddVertexAt(3, new Point2d(line2.EndPoint.X, line2.EndPoint.Y), 0, 0, 0);
                                    isOk = true;
                                }
                                if (isOk)
                                {
                                    //创建一个 Q_j 对象，将文本对象和多段线关联起来，然后添加到 qj2 列表
                                    polyline1.Closed = true;
                                    Q_j q;
                                    q.txt = ij.txt;
                                    q.pl = polyline1;
                                    qj2.Add(q);
                                    usedRelay.Add(ij.txt);
                                    isOk = false;
                                    //File.AppendAllText("qj2.txt", q.txt.Text + ": " + q.pl.GetPoint2dAt(0) + ", " + q.pl.GetPoint2dAt(2) + "\r\n");
                                    break;
                                }
                            }
                        }
                        //File.AppendAllText("x.txt", "\r\n");
                    }
                    #region 完整矩形继电器识别
                    //File.Delete("polylinejd.txt");
                    //foreach (var i in Polylines)
                    //{
                    //    double minDistance = double.MaxValue;
                    //    TextWithCoordinates resMText = null;
                    //    if (i.Closed && i.NumberOfVertices == 4 && !bx_poly.Contains(i))
                    //    {
                    //        double x = (i.GetPoint2dAt(0).X + i.GetPoint2dAt(1).X) / 2.0;
                    //        double y = (i.GetPoint2dAt(0).Y + i.GetPoint2dAt(3).Y) / 2.0;
                    //        foreach (var j in jd_txt)
                    //        {
                    //            if (!usedRelay.Contains(j))
                    //            {
                    //                double distance = Math.Sqrt(Math.Pow(x - j.Coordinates.X, 2) + Math.Pow(y - j.Coordinates.Y, 2));
                    //                if (distance < minDistance)
                    //                {
                    //                    minDistance = distance;
                    //                    resMText = j;
                    //                }
                    //            }
                    //        }
                    //    }
                    //    //if (resMText != null && resMText.Text == "鼓风机继电器")
                    //    //{
                    //    //    Console.WriteLine($"最小距离: {minDistance}");
                    //    //}
                    //    if (minDistance < 23)
                    //    {
                    //        Q_j q;
                    //        q.txt = resMText;
                    //        q.pl = i;
                    //        qj2.Add(q);
                    //        usedRelay.Add(resMText);
                    //        File.AppendAllText("polylinejd.txt", resMText.Text + q.pl.GetPoint2dAt(0) + ", " + q.pl.GetPoint2dAt(2) + "\r\n");
                    //    }
                    //}
                    //File.Delete("polylinejd.txt");

                    foreach (var txt in jd_txt)
                    {
                        double minDistance = double.MaxValue;
                        Teigha.DatabaseServices.Polyline nearestPolyline = null;

                        foreach (var pl in Polylines)
                        {
                            if (pl.Closed && pl.NumberOfVertices == 4 && !bx_poly.Contains(pl))
                            {
                                // 计算多段线中心点
                                double x = (pl.GetPoint2dAt(0).X + pl.GetPoint2dAt(1).X) / 2.0;
                                double y = (pl.GetPoint2dAt(0).Y + pl.GetPoint2dAt(3).Y) / 2.0;

                                // 计算文本与多段线中心的距离
                                double distance = Math.Sqrt(Math.Pow(x - txt.Coordinates.X, 2) + Math.Pow(y - txt.Coordinates.Y, 2));
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    nearestPolyline = pl;
                                }
                            }
                        }

                        // 如果最近距离满足阈值，建立匹配
                        if (nearestPolyline != null && minDistance < 23 && !usedRelay.Contains(txt))
                        {
                            Q_j q;
                            q.txt = txt;
                            q.pl = nearestPolyline;
                            qj2.Add(q);
                            usedRelay.Add(txt);

                            // File.AppendAllText("polylinejd.txt", $"{txt.Text} {nearestPolyline.GetPoint2dAt(0)}, {nearestPolyline.GetPoint2dAt(2)}\r\n");
                        }
                    }
                    /*                    File.Delete("qj2e.txt");
                                        foreach (var q in qj2)
                                        {
                                            // 输出格式：文本内容 + 顶点0坐标 + 顶点2坐标，换行
                                            string line = $"{q.txt.Text} {q.pl.GetPoint2dAt(0)} {q.pl.GetPoint2dAt(2)}\r\n";
                                            File.AppendAllText("qj2e.txt", line);
                                        }*/
                    #endregion
                    //初始化 jdyjtxt 列表，用于存储特定的文本对象
                    List<TextWithCoordinates> jdyjtxt = new List<TextWithCoordinates>();
                    //循环遍历 mtxts 列表
                    //根据文本内容（"85", "86", "87", "30", "87a"）将文本对象添加到 jdyjtxt 列表。
                    for (int j = 0; j < mtxts.Count; j++)
                    {
                        TextWithCoordinates newtext = new TextWithCoordinates(mtxts[j].Text, mtxts[j].Location);
                        if (mtxts[j].Text == "85")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "86")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "87")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "30")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "87a")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "1")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "2")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "3")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "4")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (mtxts[j].Text == "5")
                        {
                            jdyjtxt.Add(newtext);
                        }
                    }
                    for (int j = 0; j < txts.Count; j++)
                    {
                        TextWithCoordinates newtext = new TextWithCoordinates(txts[j].TextString, txts[j].Position);
                        if (txts[j].TextString == "85")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "86")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "87")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "30")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "87a")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "1")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "2")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "3")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "4")
                        {
                            jdyjtxt.Add(newtext);
                        }
                        if (txts[j].TextString == "5")
                        {
                            jdyjtxt.Add(newtext);
                        }
                    }
                    //初始化 myqj1 列表，用于存储最终的关联对象，包括继电器文本、多段线和附加的特定文本对象
                    List<Myqj> myqj1 = new List<Myqj>();
                    //循环遍历 qj2 列表
                    //File.Delete("myqj1.txt");
                    //foreach (var ij in qj2)
                    //{
                    //    //对于每个 Q_j 对象，创建一个 Myqj 对象，将附加的特定文本对象添加到 Myqj 对象的 yj 列表中。
                    //    Myqj q1;
                    //    q1.yj = new List<TextWithCoordinates>();
                    //    foreach (var i in jdyjtxt)
                    //    {
                    //        //使用 GetDistanceToPolyline 函数计算文本对象与多段线的距离，如果距离小于10，则将文本对象添加到 yj 列表。
                    //        if (GetDistanceToPolyline(i.Coordinates, ij.pl) < 10f)
                    //        {
                    //            q1.yj.Add(i);
                    //        }
                    //    }
                    //    //如果 yj 列表不为空，将 Myqj 对象添加到 myqj1 列表
                    //    if (q1.yj.Count != 0)
                    //    {
                    //        q1.pl = ij.pl;
                    //        q1.txt = ij.txt;
                    //        myqj1.Add(q1);
                    //    }
                    //}

                    foreach (var ij in qj2)
                    {
                        Myqj q1;
                        q1.yj = new List<TextWithCoordinates>();
                        foreach (var relayPinText in jdyjtxt)
                        {
                            // 继电器端子文字通常位于轮廓内部或紧邻边界。
                            // 保留轮廓附近的候选，寻径到继电器时再按入口点选择具体端子。
                            if (GetDistanceToPolyline(relayPinText.Coordinates, ij.pl) < 10f)
                            {
                                q1.yj.Add(relayPinText);
                            }
                        }
                        q1.pl = ij.pl;
                        q1.txt = ij.txt;
                        myqj1.Add(q1);
                    }

                    #region 复合实体继电器
                    //File.Delete("blockArea.txt");
                    // 图纸中可能不存在可识别的继电器轮廓，此时不能用 Last() 取样。
                    if (myqj1.Count > 0)
                    {
                        double areaRelay = myqj1[myqj1.Count - 1].pl.Area;
                        foreach (var i in blocks)
                        {
                            Teigha.DatabaseServices.Polyline polyline1 = new Teigha.DatabaseServices.Polyline();
                            double minDistance = double.MaxValue;
                            TextWithCoordinates resMText = null;
                            double lengthBlocks = i.Bounds.Value.MaxPoint.X - i.Bounds.Value.MinPoint.X;
                            double widthBlocks = i.Bounds.Value.MaxPoint.Y - i.Bounds.Value.MinPoint.Y;
                            double area = lengthBlocks * widthBlocks;
                            //File.AppendAllText("blockArea.txt", area + "\r\n");
                            if (Math.Abs(area - areaRelay) < 80)
                            {
                                polyline1.AddVertexAt(0, new Point2d(i.Bounds.Value.MinPoint.X, i.Bounds.Value.MaxPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(1, new Point2d(i.Bounds.Value.MaxPoint.X, i.Bounds.Value.MaxPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(2, new Point2d(i.Bounds.Value.MaxPoint.X, i.Bounds.Value.MinPoint.Y), 0, 0, 0);
                                polyline1.AddVertexAt(3, new Point2d(i.Bounds.Value.MinPoint.X, i.Bounds.Value.MinPoint.Y), 0, 0, 0);
                                polyline1.Closed = true;
                                double x = (i.Bounds.Value.MinPoint.X + i.Bounds.Value.MaxPoint.X) / 2.0;
                                double y = (i.Bounds.Value.MaxPoint.Y + i.Bounds.Value.MinPoint.Y) / 2.0;
                                foreach (var j in jd_txt)
                                {
                                    if (!usedRelay.Contains(j))
                                    {
                                        double distance = Math.Sqrt(Math.Pow(x - j.Coordinates.X, 2) + Math.Pow(y - j.Coordinates.Y, 2));
                                        if (distance < minDistance)
                                        {
                                            minDistance = distance;
                                            resMText = j;
                                        }
                                    }
                                }
                            }
                            if (minDistance < 15)
                            {
                                Myqj q1;
                                q1.yj = new List<TextWithCoordinates>();
                                q1.pl = polyline1;
                                q1.txt = resMText;
                                myqj1.Add(q1);
                                usedRelay.Add(resMText);
                                //File.AppendAllText("polylinejd.txt", resMText.Text + polyline1.GetPoint2dAt(0) + ", " + polyline1.GetPoint2dAt(2) + "\r\n");
                            }
                        }
                    }

                    // 遍历 myqj1 列表中的每个 Myqj 对象
                    /*                    foreach (var qj in myqj1)
                                        {
                                            // 获取 Myqj 对象的文本内容和坐标
                                            string text = qj.txt.Text;  // 获取文本内容
                                            Point3d coordinates = qj.txt.Coordinates;  // 获取文本的坐标

                                            // 将文本和坐标信息写入文件
                                            File.AppendAllText("myqj1.txt", $"{text}，坐标：({coordinates.X}, {coordinates.Y}, {coordinates.Z})\r\n");
                                        }*/
                    #endregion
                    /*                    foreach(var i in myqj1)
                                        {
                                            File.AppendAllText("myqj1.txt", i.txt.Text + "\r\n");
                                        }*/
                    //初始化 nmyl1 列表，用于存储经过处理后的线段列表
                    List<my_line> nmyl1 = new List<my_line>();
                    //将 mylines 列表中的所有线段复制到 nmyl1 列表。
                    foreach (var i in mylines)
                    {
                        nmyl1.Add(i);
                    }

                    //删除继电器内部的线
                    foreach (var ij in qj2)
                    {
                        foreach (var i in mylines)
                        {
                            //使用 IsPointOnPolylineBoundary 函数检查线段的端点是否在多段线的边界上
                            if (IsPointOnPolylineBoundary(ij.pl, i.line.StartPoint) && IsPointInsideRectangle(i.line.EndPoint, ij.pl))
                            {
                                //如果满足条件，从 nmyl1 列表中移除该线段。
                                nmyl1.Remove(i);
                                //File.AppendAllText("x1.txt", "圆 " + i.line.StartPoint+" "+i.line.EndPoint+ "\r\n");
                            }
                            //使用 IsPointInsideRectangle 函数检查线段的端点是否在多段线的矩形区域内
                            if (IsPointOnPolylineBoundary(ij.pl, i.line.EndPoint) && IsPointInsideRectangle(i.line.StartPoint, ij.pl))
                            {
                                //File.AppendAllText("x1.txt", "圆 " + i.line.StartPoint + " " + i.line.EndPoint + "\r\n");
                                //如果满足条件，从 nmyl1 列表中移除该线段。
                                nmyl1.Remove(i);
                            }
                        }
                    }
                    // 删除保险丝内部线
                    foreach (var ij in bp)
                    {
                        foreach (var i in mylines)
                        {
                            //使用 IsPointOnPolylineBoundary 函数检查线段的端点是否在多段线的边界上
                            if (IsPointOnPolylineBoundary(ij.pl, i.line.StartPoint) && IsPointInsideRectangle(i.line.EndPoint, ij.pl))
                            {
                                //如果满足条件，从 nmyl1 列表中移除该线段。
                                nmyl1.Remove(i);
                                //File.AppendAllText("x1.txt", "圆 " + i.line.StartPoint+" "+i.line.EndPoint+ "\r\n");
                            }
                            //使用 IsPointInsideRectangle 函数检查线段的端点是否在多段线的矩形区域内
                            if (IsPointOnPolylineBoundary(ij.pl, i.line.EndPoint) && IsPointInsideRectangle(i.line.StartPoint, ij.pl))
                            {
                                //File.AppendAllText("x1.txt", "圆 " + i.line.StartPoint + " " + i.line.EndPoint + "\r\n");
                                //如果满足条件，从 nmyl1 列表中移除该线段。
                                nmyl1.Remove(i);
                            }
                        }
                    }

                    mylines = nmyl1;
                    #endregion
                    #region M水泵
                    List<MText> sbtxt = new List<MText>();
                    for (int j = 0; j < mtxts.Count; j++)
                    {
                        if (mtxts[j].Text == "M")
                        {
                            sbtxt.Add(mtxts[j]);
                            //File.AppendAllText("x1.txt", i.Text + i.Location.X + " " + i.Location.Y + "\r\n");
                        }

                    }

                    #endregion

                    #region 制动开关（特化）
                    List<MText> szdxt = new List<MText>();
                    for (int j = 0; j < mtxts.Count; j++)
                    {
                        if (mtxts[j].Text == "制动开关")
                        {
                            szdxt.Add(mtxts[j]);
                            //File.AppendAllText("x1.txt", i.Text + i.Location.X + " " + i.Location.Y + "\r\n");
                        }

                    }
                    #endregion


                    List<Point3d> mnpt = new List<Point3d>();
                    List<Point3d> otpt = new List<Point3d>();
                    List<int> lj = new List<int>();
                    List<string> ljbx = new List<string>();
                    Hashtable fz = new Hashtable();
                    string clbx;
                    #region 点在矩形poly边界的判断
                    /*                    double GetDistanceToPolyline(Point3d point, Teigha.DatabaseServices.Polyline polyline)
                                        {
                                            double minDistance = double.MaxValue;

                                            for (int i = 0; i < polyline.NumberOfVertices; i++)
                                            {
                                                if (i == polyline.NumberOfVertices - 1)
                                                {
                                                    LineSegment3d segment = new LineSegment3d(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(0));

                                                    double distance = segment.GetDistanceTo(point);
                                                    if (distance < minDistance)
                                                    {
                                                        minDistance = distance;
                                                    }
                                                }
                                                else
                                                {
                                                    LineSegment3d segment = new LineSegment3d(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(i + 1));

                                                    double distance = segment.GetDistanceTo(point);
                                                    if (distance < minDistance)
                                                    {
                                                        minDistance = distance;
                                                    }
                                                }

                                            }

                                            return minDistance;
                                        }*/
                    double GetDistanceToPolyline(Point3d point, Teigha.DatabaseServices.Polyline polyline)
                    {
                        if (polyline == null || polyline.NumberOfVertices < 2)
                        {
                            return double.MaxValue;
                        }

                        double minDistance = double.MaxValue;
                        int segmentCount = polyline.Closed
                            ? polyline.NumberOfVertices
                            : polyline.NumberOfVertices - 1;

                        for (int i = 0; i < segmentCount; i++)
                        {
                            int nextIndex = (i + 1) % polyline.NumberOfVertices;
                            LineSegment3d segment = new LineSegment3d(
                                polyline.GetPoint3dAt(i),
                                polyline.GetPoint3dAt(nextIndex));
                            double distance = segment.GetDistanceTo(point);
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                            }
                        }

                        return minDistance;
                    }
                    bool IsPointOnPolylineBoundary(Teigha.DatabaseServices.Polyline polyline, Point3d point)
                    {
                        if (GetDistanceToPolyline(point, polyline) < 1f)
                        {
                            return true;
                        }
                        return false;
                    }
                    Point3d getRelayInputPoint(Teigha.DatabaseServices.Polyline polyline, Point3d point)
                    {
                        Point2d startPoint;
                        Point3d InputPoint = new Point3d(point.X, polyline.GetPoint2dAt(0).Y, 0);
                        return InputPoint;
                    }
                    int GetPointOnPolylineBoundaryIndex(Teigha.DatabaseServices.Polyline polyline, Point3d point)
                    {
                        for (int i = 0; i < polyline.NumberOfVertices; i++)
                        {
                            if (i == polyline.NumberOfVertices - 1)
                            {
                                LineSegment3d segment = new LineSegment3d(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(0));
                                if (segment.IsOn(point))
                                {
                                    return i;
                                }
                            }
                            else
                            {
                                LineSegment3d segment = new LineSegment3d(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(i + 1));
                                if (segment.IsOn(point))
                                {
                                    return i;
                                }
                            }

                        }

                        return -1; // 表示点不在多段线的任何一条边界上
                    }
                    File.Delete("debugtest.txt");
                    Point3d GetPointOnOppositeEdge(Teigha.DatabaseServices.Polyline polyline, Point3d point)
                    {
                        int index = GetPointOnPolylineBoundaryIndex(polyline, point);
                        if (index != -1)
                        {
                            //File.AppendAllText("debugtest.txt", "正确，能找到对面点" + "\r\n");
                            //File.AppendAllText("debugtest.txt", point + "\r\n");
                            int oppositeIndex = (index + 2) % polyline.NumberOfVertices;
                            Point3d startPoint = polyline.GetPoint3dAt(oppositeIndex);
                            Point3d endPoint = polyline.GetPoint3dAt((oppositeIndex + 1) % polyline.NumberOfVertices);

                            if (startPoint.X == endPoint.X) // 平行于Y轴
                            {
                                //File.AppendAllText("debugtest.txt", startPoint.X + " " + point.Y + "\r\n");
                                return new Point3d(startPoint.X, point.Y, 0);
                            }
                            else if (startPoint.Y == endPoint.Y) // 平行于X轴
                            {
                                //File.AppendAllText("debugtest.txt", point.X + " " + startPoint.Y + "\r\n");
                                return new Point3d(point.X, startPoint.Y, 0);
                            }
                        }

                        return Point3d.Origin; // 表示未找到符合条件的点
                        //File.AppendAllText("debugtest.txt", "错误，不能找到对面点" + "\r\n");
                    }
                    Point3d GetDiagonalPointOnOppositeEdge(Teigha.DatabaseServices.Polyline polyline, Point3d point)
                    {
                        // 获取输入点在矩形边界中的索引
                        int index = GetPointOnPolylineBoundaryIndex(polyline, point);
                        if (index != -1)
                        {
                            // 获取输入点所在边的两个端点
                            Point3d startPoint = polyline.GetPoint3dAt(index);
                            Point3d endPoint = polyline.GetPoint3dAt((index + 1) % polyline.NumberOfVertices);

                            // 计算输入点相对于该边的偏移量
                            double offsetX = point.X - startPoint.X;
                            double offsetY = point.Y - startPoint.Y;

                            // 获取矩形对面边的两个端点
                            int oppositeIndex = (index + 2) % polyline.NumberOfVertices;
                            Point3d oppositeStart = polyline.GetPoint3dAt(oppositeIndex);
                            Point3d oppositeEnd = polyline.GetPoint3dAt((oppositeIndex + 1) % polyline.NumberOfVertices);

                            // 计算斜对角点，根据对面边的位置应用偏移量
                            if (oppositeStart.X == oppositeEnd.X) // 对面边平行于 Y 轴
                            {
                                //File.AppendAllText("debugtest.txt", oppositeStart.X + " " + (oppositeStart.Y - offsetY) + "\r\n");
                                return new Point3d(oppositeStart.X, oppositeStart.Y - offsetY, 0); // 偏移应用于 X 坐标
                            }
                            else if (oppositeStart.Y == oppositeEnd.Y) // 对面边平行于 X 轴
                            {
                                //File.AppendAllText("debugtest.txt", (oppositeStart.X - offsetX) + " " + oppositeStart.Y + "\r\n");
                                return new Point3d(oppositeStart.X - offsetX, oppositeStart.Y, 0); // 偏移应用于 Y 坐标
                            }
                        }

                        return Point3d.Origin; // 输入点不在矩形的边上，返回原点
                    }

                    #endregion

                    bool bre = false;
                    int bxsjs = 0;
                    int midlj = -1;

                    #region 寻径制动开关
                    //用于处理点集合中的点，并根据一定的条件生成新的点集合
                    void getzdq()
                    {
                        //将 mnpt（可能是当前点集）赋值给 otpt（可能是输出点集）
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        //循环遍历 mnpt 中的每个点
                        foreach (var i in mnpt)
                        {
                            //标记是否已经为当前点 i 找到了匹配的点并添加到了 otpt
                            bool haveout = false;
                            Point3d start = i;
                            //内部循环遍历另一个点集 szdxt
                            foreach (var ij in szdxt)
                            {
                                //使用 Math.Abs 函数计算点 start（即 mnpt 中的当前点 i）与 szdxt 中每个点 ij 的 X 和 Y 坐标差的绝对值，并与特定的数值（12.34 和 7）进行比较，检查是否在一定的误差范围内（0.1f 和 1f）。
                                if (Math.Abs(Math.Abs(start.X - ij.Location.X) - 12.34) < 0.1f && Math.Abs(Math.Abs(start.Y - ij.Location.Y) - 7) < 1f)
                                {
                                    //据条件创建一个新的 Point3d 对象 newop，其 X 和 Y 坐标与 start 相同，但 Y 坐标减去 9.6，Z 坐标保持不变
                                    Point3d newop = new Point3d(start.X, start.Y - 9.6, start.Z);
                                    //将新创建的点 newop 添加到 otpt 中
                                    otpt.Add(newop);
                                    bre = true;
                                    //将 haveout 设置为 true 并退出内部循环
                                    haveout = true;
                                    break;
                                }

                            }
                            //如果内部循环结束后 haveout 仍为 false，说明没有找到符合条件的点，那么直接将原始点 i 添加到 otpt。
                            if (!haveout)
                                otpt.Add(i);
                        }

                    }
                    #endregion

                    #region 寻径水泵
                    void getsb()
                    {
                        //msbxt 被初始化为 sbtxt 的副本，sbtxt 是一个包含水泵文本对象的列表。
                        List<MText> smsbxt = sbtxt;
                        //mnpt 被赋值为 otpt，这里 mnpt 可能是一个点集, 制动开关筛选后剩下的点集列表，用于存储新生成的点。
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        //遍历 mnpt 中的每个点 i
                        foreach (var i in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = i;
                            //对于 mnpt 中的每个点，遍历 sbtxt 中的每个文本对象 ij
                            foreach (var ij in sbtxt)
                            {
                                //如果点 i 的 X 坐标与文本对象 ij 的 X 坐标差的绝对值小于 0.1，并且 Y 坐标差的绝对值小于 4。
                                if (Math.Abs(start.X - ij.Location.X) < 0.1f && Math.Abs(start.Y - ij.Location.Y) < 4f)
                                {
                                    Point3d newop = new Point3d(start.X, 2 * ij.Location.Y - start.Y, start.Z);
                                    otpt.Add(newop);
                                    smsbxt.Remove(ij);
                                    bre = true;
                                    haveout = true;
                                    File.AppendAllText("x.txt", "大水泵" + "\r\n");
                                    break;
                                }
                                //点 i 的 Y 坐标与文本对象 ij 的 Y 坐标差的绝对值小于 0.1，并且 X 坐标差的绝对值小于 4，那么认为这两个对象是相关的
                                if (Math.Abs(start.Y - ij.Location.Y) < 0.1f && Math.Abs(start.X - ij.Location.X) < 4f)
                                {
                                    Point3d newop = new Point3d(2 * ij.Location.X - start.X, start.Y, start.Z);
                                    otpt.Add(newop);
                                    smsbxt.Remove(ij);
                                    bre = true;
                                    haveout = true;
                                    File.AppendAllText("x.txt", "大水泵" + "\r\n");
                                    break;
                                }
                            }
                            if (!haveout)
                                otpt.Add(i);
                        }
                        sbtxt = smsbxt;
                    }
                    #endregion

                    #region 保险丝出入点（中点）
                    File.Delete("bxsPoint.txt");
                    void getbxs()
                    {
                        //midbp 被初始化为 bp 的副本，bp 是一个包含 bx_pl 类型对象的列表，每个对象至少包含 top 和 down 两个 Point3d 类型的点
                        List<bx_pl> midbp = bp;
                        //mnpt 被赋值为 otpt，这里 mnpt 可能是一个点集
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        //遍历 mnpt 中的每个点 i
                        foreach (var i in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = i;
                            //对于 mnpt 中的每个点，遍历 bp 中的每个 bx_pl 对象 ij
                            foreach (var ij in bp)
                            {
                                bool bxsflag = false;
                                //如果点 i 与 bx_pl 对象的 top 点或 down 点的 X 和 Y 坐标差的绝对值都小于 0.1，认为这两个点是“接近”的。
                                if ((Math.Abs(start.X - ij.top.X) < 0.1f && Math.Abs(start.Y - ij.top.Y) < 0.1f) || (start.Y <= ij.top.Y && start.Y >= ij.down.Y && Math.Abs(start.X - ij.top.X) < 0.2f))
                                {
                                    //如果找到接近的点，从 midbp 中移除对应的 bx_pl 对象 ij
                                    midbp.Remove(ij);
                                    foreach (var myline in mylines)
                                    {
                                        if (GetPointIsInLine(ij.down, myline.line.StartPoint, myline.line.EndPoint, 0.1) && IsPointInsideRectangle(myline.line.StartPoint, ij.pl))
                                        {
                                            bxsflag = true;
                                            otpt.Add(myline.line.EndPoint);
                                            //mylines.Remove(myline);
                                            //File.AppendAllText("bxsPoint.txt", ij.txt.Text + "输出的线的点：" + myline.line.EndPoint + "\r\n");
                                            // 创建 mylines 的副本
                                            foreach (var j in mylines.ToList())
                                            {
                                                if (Math.Abs(j.line.StartPoint.X - myline.line.StartPoint.X) < 0.2f &&
                                                    Math.Abs(j.line.EndPoint.X - myline.line.EndPoint.X) < 0.2f)
                                                {
                                                    mylines.Remove(j);
                                                }
                                            }
                                            break;
                                        }
                                    }
                                    if (bxsflag == false)
                                    {
                                        otpt.Add(ij.down);
                                    }
                                    bre = true;
                                    haveout = true;
                                    clbx = ij.bxtext.Text;
                                    if (lj.Count > 0)
                                    {
                                        lj[mnpt.IndexOf(i)]++;
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + lj[mnpt.IndexOf(i)] + "级保险" + ij.bxtext.Text + "\r\n");

                                    }
                                    else
                                    {
                                        bxsjs++;
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + bxsjs + "级保险" + ij.bxtext.Text + "\r\n");
                                    }
                                    break;
                                }
                            }
                            //未匹配的点直接将原始点 i 添加到 otpt
                            if (!haveout)
                                otpt.Add(i);
                        }
                        bp = midbp;



                    }
                    #endregion

                    #region 寻径circle保险丝
                    File.Delete("li.txt");
                    void getcirbxs()
                    {
                        //mnpt 被赋值为 otpt，这里 mnpt 和 otpt 都是 List<Point3d> 类型，分别代表中间点集和输出点集。
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        //遍历 mnpt 中的每个点 i
                        foreach (var i in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = i;
                            bool isfinish = false;
                            double r = 0;
                            int outputLevel = bxsjs;
                            for (int fuseIndex = 0; fuseIndex < new_bp.Count; fuseIndex++)
                            {
                                bx_cir ij = new_bp[fuseIndex];
                                Point3d nextPoint = new Point3d();
                                //如果点 i 与 new_bp 对象的 top 的 Center 点或 down 的 Center 点的 X 和 Y 坐标差的绝对值都小于 0.1，认为这两个点是“接近”的
                                if (Math.Abs(start.X - ij.top.Center.X) < 0.2f && Math.Abs(start.Y - ij.top.Center.Y) < ij.top.Radius + 0.2f)
                                {
                                    //如果 lj 列表（可能是一个索引列表）不为空，并且其大小大于 0，将 lj 中与点 i 在 mnpt 中索引相对应的元素加 1，并写入文本信息到 "x.txt" 文件
                                    if (lj.Count > 0)
                                    {
                                        lj[mnpt.IndexOf(i)]++;
                                        outputLevel = lj[mnpt.IndexOf(i)];
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + lj[mnpt.IndexOf(i)] + "级cir保险" + ij.cirText.Text + "\r\n");

                                    }
                                    //如果 lj 为空或大小为 0，增加 bxsjs 计数器，并写入文本信息到 "x.txt" 文件
                                    else
                                    {
                                        bxsjs++;
                                        outputLevel = bxsjs;
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + bxsjs + "级cir保险" + ij.cirText.Text + "\r\n");
                                    }
                                    clbx = ij.cirText.Text;
                                    nextPoint = ij.down.Center;
                                    isfinish = true;
                                    r = ij.down.Radius;
                                }
                                //相似处理
                                else if (Math.Abs(start.X - ij.down.Center.X) < 0.2f && Math.Abs(start.Y - ij.down.Center.Y) < ij.down.Radius + 0.2f)
                                {
                                    // otpt.Add(ij.top.Center);
                                    // bre = true;
                                    // haveout = true;
                                    if (lj.Count > 0)
                                    {
                                        lj[mnpt.IndexOf(i)]++;
                                        outputLevel = lj[mnpt.IndexOf(i)];
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + lj[mnpt.IndexOf(i)] + "级cir保险" + ij.cirText.Text + "\r\n");

                                    }

                                    else
                                    {
                                        bxsjs++;
                                        outputLevel = bxsjs;
                                        //这里有加入文本信息
                                        File.AppendAllText("x.txt", "第" + bxsjs + "级cir保险" + ij.cirText.Text + "\r\n");
                                    }
                                    clbx = ij.cirText.Text;
                                    nextPoint = ij.top.Center;
                                    isfinish = true;
                                    r = ij.top.Radius;
                                }
                                if (isfinish)
                                {
                                    // 往下寻径
                                    for (int lineIndex = 0; lineIndex < mylines.Count; lineIndex++)
                                    {
                                        my_line myline = mylines[lineIndex];
                                        //检查 my_line 对象的 visit 属性是否为 false 且线段长度大于 2.03，如果是，则认为是保险丝内部的线段
                                        if (myline.line.Length > 2.03)//保险丝内部line
                                        {
                                            if (Math.Abs(nextPoint.Y - myline.line.StartPoint.Y) < r + 0.2f && Math.Abs(nextPoint.X - myline.line.StartPoint.X) < 0.2f)
                                            {
                                                otpt.Add(myline.line.EndPoint);
                                                lj.Add(outputLevel);
                                                File.AppendAllText("li.txt", "线的出点" + myline.line.EndPoint + "\r\n");
                                                bre = true;
                                                haveout = true;
                                                mylines.RemoveAt(lineIndex);
                                                break;
                                            }
                                            else if (Math.Abs(nextPoint.Y - myline.line.EndPoint.Y) < r + 0.2f && Math.Abs(nextPoint.X - myline.line.EndPoint.X) < 0.2f)
                                            {
                                                otpt.Add(myline.line.StartPoint);
                                                lj.Add(outputLevel);
                                                File.AppendAllText("li.txt", "线的出点" + myline.line.StartPoint + "\r\n");
                                                bre = true;
                                                haveout = true;
                                                mylines.RemoveAt(lineIndex);
                                                break;
                                            }
                                        }
                                    }
                                    // 圆形保险丝的另一端可能直接连接圆弧。即使没有找到直线，
                                    // 也必须把遍历点推进到另一端，交给后续 getli/getarc 继续寻径。
                                    if (!haveout)
                                    {
                                        otpt.Add(nextPoint);
                                        lj.Add(outputLevel);
                                        haveout = true;
                                    }
                                    break;
                                }
                            }
                            //未匹配的点
                            if (!haveout)
                                otpt.Add(i);
                        }
                    }
                    #endregion

                    #region 获取末端负载(不调整bre
                    void getqj()
                    {
                        //将 otpt 列表赋值给 mnpt 变量
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        foreach (var iji in mnpt)
                        {
                            bool ffl = false;
                            Point3d start = iji;
                            //遍历 mnpt 列表中的每一个 Point3d 对象
                            foreach (var i in myqj)
                            {
                                //调用 IsPointOnPolylineBoundary 函数检查点 start 是否位于 i.pl 的边界上
                                if (IsPointOnPolylineBoundary(i.pl, start) || (DistancePointToLineSegment(start, i.pl.GetPoint3dAt(0), i.pl.GetPoint3dAt(1)) < 1.5 && start.X > i.pl.GetPoint3dAt(0).X && start.X < i.pl.GetPoint3dAt(1).X))
                                {
                                    File.AppendAllText("x.txt", "对应保险丝" + clbx + "\r\n");
                                    ffl = true;
                                    //这里有加入文本信息
                                    //如果点在边界上，将文本信息写入到 "x.txt" 文件中。
                                    File.AppendAllText("x.txt", "负载" + i.txt.Text + "\r\n");
                                    //在这里是填写的大的矩形模块的名称，可不可以在这里添加内部更细的负载名
                                    foreach (var ij in i.yj)
                                    {
                                        //检查 start 点与 ij.Location 的 X 和 Y 坐标差是否在一定的阈值内,是否属于负载的引脚
                                        if (Math.Abs(start.X - ij.Coordinates.X) < 6.56f && Math.Abs(start.Y - ij.Coordinates.Y) < 6.56f)
                                        {
                                            //这里有加入文本信息
                                            File.AppendAllText("x.txt", "引脚" + ij.Text + "\r\n");
                                            break;
                                        }

                                    }
                                    //这里有加入文本信息这不是判断lj.Count，但是判断IsPointOnPolylineBoundary(i.pl, start)                                
                                    break;
                                }
                            }
                            //添加未匹配的到列表中
                            if (!ffl)
                            {
                                otpt.Add(iji);
                            }
                        }
                    }
                    #endregion

                    #region 圆弧
                    void getarc()
                    {
                        //圆弧数据
                        List<Arc> midac = arcs;
                        //上一次未匹配的点集
                        mnpt = otpt;
                        //存储这次未匹配的点集
                        otpt = new List<Point3d>();
                        //遍历 mnpt 列表中的每个 Point3d 对象
                        foreach (var iji in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = iji;
                            //遍历 arcs 列表中的每个 Arc 对象
                            foreach (var i in arcs)
                            {
                                //如果 start 点与 Arc 对象的起点 i.StartPoint 在 X 和 Y 坐标上的距离小于0.1，则认为 start 是圆弧的起点。
                                if (Math.Abs(start.X - i.StartPoint.X) < 0.1f && Math.Abs(start.Y - i.StartPoint.Y) < 0.1f)
                                {
                                    midac.Remove(i);
                                    otpt.Add(i.EndPoint);
                                    //File.AppendAllText("x.txt", "线的出点" + i.line.EndPoint + "\r\n");
                                    bre = true;
                                    haveout = true;
                                    break;
                                }
                                //终点也是进行一样的判断
                                else if (Math.Abs(start.X - i.EndPoint.X) < 0.1f && Math.Abs(start.Y - i.EndPoint.Y) < 0.1f)
                                {
                                    midac.Remove(i);
                                    otpt.Add(i.StartPoint);
                                    //File.AppendAllText("x.txt", "线的入点" + i.line.StartPoint + "\r\n");
                                    bre = true;
                                    haveout = true;
                                    break;
                                }

                            }
                            //没有匹配的就放到这个变量里
                            if (!haveout)
                                otpt.Add(iji);
                        }
                        arcs = midac;
                    }
                    #endregion

                    #region 线的出入判断（仅首尾）
                    //File.Delete("debug1.txt");
                    void getli()
                    {
                        Hashtable hs = new Hashtable();
                        List<my_line> midlin = new List<my_line>();
                        List<my_line> midls = mylines;
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        //遍历 mnpt 列表中的每个 Point3d 对象
                        foreach (var iji in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = iji;
                            //遍历 mylines 列表中的每个 my_line 对象
                            foreach (var i in mylines)
                            {
                                //检查 my_line 对象的 visit 属性是否为 false 且线段长度大于 2.03，如果是，则认为是保险丝内部的线段
                                if (!i.visit && i.line.Length > 2.03)//保险丝内部line
                                {
                                    //检查 start 点是否接近 my_line 对象的起点或终点
                                    if (Math.Abs(start.Y - i.line.StartPoint.Y) < 0.1f && Math.Abs(start.X - i.line.StartPoint.X) < 0.1f)
                                    {
                                        if (!hs.ContainsKey(i.line.EndPoint))
                                            hs.Add(i.line.EndPoint, i.line.StartPoint);
                                        midlin.Add(i);
                                        midls.Remove(i);
                                        otpt.Add(i.line.EndPoint);
                                        lj.Add(bxsjs);
                                        //File.AppendAllText("li.txt", "线的出点" + i.line.EndPoint + "\r\n")
                                        bre = true;
                                        haveout = true;
                                        break;
                                    }
                                    else if (Math.Abs(start.Y - i.line.EndPoint.Y) < 0.1f && Math.Abs(start.X - i.line.EndPoint.X) < 0.1f)
                                    {
                                        if (!hs.ContainsKey(i.line.EndPoint))
                                            hs.Add(i.line.EndPoint, i.line.StartPoint);
                                        midlin.Add(i);
                                        midls.Remove(i);
                                        otpt.Add(i.line.StartPoint);
                                        lj.Add(bxsjs);
                                        //File.AppendAllText("li.txt", "线的出点" + i.line.StartPoint + "\r\n");
                                        bre = true;
                                        haveout = true;
                                        break;
                                    }
                                }
                            }
                            //未匹配线段
                            if (!haveout)
                            {
                                otpt.Add(iji);
                                lj.Add(bxsjs);
                            }

                        }
                        //将更新后的 midls 列表赋值回 mylines
                        mylines = midls;
                        //遍历 midlin 列表中的每个 my_line 对象
                        foreach (var i in midlin)
                        {
                            //遍历 mylines 列表中的每个 my_line 对象
                            foreach (var ij in mylines)
                            {
                                //是否与 midlin 中的线段有接近的点，但不是相同的端点
                                if (!(ij.line.StartPoint.X == i.line.StartPoint.X && ij.line.StartPoint.Y == i.line.StartPoint.Y && ij.line.EndPoint.Y == i.line.EndPoint.Y && ij.line.EndPoint.X == i.line.EndPoint.X))
                                {
                                    //点到线段的距离小于0.2，并且点不是线段的端点，则将 midlin 中的端点添加到 otpt 并添加 bxsjs 到 lj
                                    if (DistancePointToLineSegment(ij.line.StartPoint, i.line.StartPoint, i.line.EndPoint) < 0.2f && (ij.line.StartPoint != i.line.StartPoint && ij.line.StartPoint != i.line.EndPoint))
                                    {
                                        otpt.Add(ij.line.EndPoint);
                                        //File.AppendAllText("x.txt", "中间点" + ij.line.StartPoint + "\r\n");
                                        lj.Add(bxsjs);
                                    }
                                }

                            }
                        }
                    }
                    #endregion


                    #region 继电器出入
                    File.Delete("OPpoints.txt");
                    void getjdq()
                    {
                        mnpt = otpt;
                        otpt = new List<Point3d>();
                        Point3d mm;

                        foreach (var i in mnpt)
                        {
                            bool haveout = false;
                            Point3d start = i;
                            //File.AppendAllText("x.txt", "点 " + start.X+" "+start.Y + "\r\n");
                            foreach (var ij in myqj1)
                            {
                                //调用 IsPointOnPolylineBoundary 函数检查点 start 是否位于 ij.pl 的边界上
                                if (IsPointOnPolylineBoundary(ij.pl, start) || IsPointInsideRectangle(start, ij.pl))
                                {
                                    //File.AppendAllText("OPpoints.txt", ij.txt.Text + "\r\n");
                                    Point3d InputPoint;
                                    if (IsPointOnPolylineBoundary(ij.pl, start))
                                    {
                                        InputPoint = start;
                                    }
                                    else
                                    {
                                        InputPoint = getRelayInputPoint(ij.pl, start);
                                    }
                                    //返回给定多段线的对面上的点
                                    Point3d mm1 = GetDiagonalPointOnOppositeEdge(ij.pl, InputPoint);
                                    Point3d mm2 = GetPointOnOppositeEdge(ij.pl, InputPoint);
                                    double widthRelay = ij.pl.GetPoint2dAt(0).Y - ij.pl.GetPoint2dAt(3).Y;
                                    //File.AppendAllText("OPpoints.txt", "对面的点：" + mm2 + "\r\n");
                                    //File.AppendAllText("OPpoints.txt", "斜对面的点：" + mm1 + "\r\n");
                                    if (Math.Abs(start.Y - mm1.Y) >= width / 2 && Math.Abs(start.Y - mm2.Y) >= width / 2)
                                    {
                                        // 直接寻径到出口所连接的线的终点，不等待下一轮循环找线
                                        foreach (var myline in mylines)
                                        {
                                            if (GetPointIsInLine(mm1, myline.line.StartPoint, myline.line.EndPoint, 0.1))
                                            {
                                                otpt.Add(myline.line.EndPoint);
                                                //File.AppendAllText("OPpoints.txt", "输出的线的点：" + myline.line.EndPoint + "\r\n");
                                            }
                                            if (GetPointIsInLine(mm2, myline.line.StartPoint, myline.line.EndPoint, 0.1))
                                            {
                                                //File.AppendAllText("OPpoints.txt", "输出的线的点：" + myline.line.EndPoint + "\r\n");
                                                otpt.Add(myline.line.EndPoint);
                                            }
                                        }
                                        //File.AppendAllText("OPpoints.txt",  "\r\n");
                                        bre = true;
                                        haveout = true;
                                        //这里有加入文本信息
                                        //File.AppendAllText("x.txt", "JDQ " + ij.txt.Text + "\r\n");
                                        File.AppendAllText("x.txt", ij.txt.Text + "\r\n");
                                        // 同一个继电器的多个端子文字可能同时落入 4x4 的容差框。
                                        // 旧逻辑取实体枚举中的第一个，结果会随 DWG 内部顺序变化，
                                        // 例如把入口端 30 误选成 87、把 5 误选成 3。
                                        TextWithCoordinates nearestRelayPin = null;
                                        double nearestRelayPinDistance = double.MaxValue;
                                        foreach (var iji in ij.yj)
                                        {
                                            double deltaX = InputPoint.X - iji.Coordinates.X;
                                            double deltaY = InputPoint.Y - iji.Coordinates.Y;
                                            if (Math.Abs(deltaX) < 4f && Math.Abs(deltaY) < 4f)
                                            {
                                                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                                                if (distance < nearestRelayPinDistance)
                                                {
                                                    nearestRelayPinDistance = distance;
                                                    nearestRelayPin = iji;
                                                }
                                            }
                                        }
                                        if (nearestRelayPin != null)
                                        {
                                            File.AppendAllText("x.txt", "引脚" + nearestRelayPin.Text + "\r\n");
                                        }
                                        break;
                                    }
                                }
                            }
                            if (!haveout)
                                otpt.Add(i);
                        }

                    }
                    #endregion

                    #region 遍历每个分支点（需要器件识别）
                    File.Delete("otpt.txt");
                    //et_line.AddRange(st_line);
                    // 每条主线必须在独立的图副本上寻径。寻径函数会移除已经走过的
                    // 线、圆弧和器件；若跨主线共享这些集合，前一条路径会污染后一条路径。
                    List<my_line> traversalLines = new List<my_line>(mylines);
                    List<Arc> traversalArcs = new List<Arc>(arcs);
                    List<bx_pl> traversalPolylineFuses = new List<bx_pl>(bp);
                    List<MText> traversalPumpTexts = new List<MText>(sbtxt);
                    foreach (var i in st_line)
                    {

                        mylines = new List<my_line>(traversalLines);
                        arcs = new List<Arc>(traversalArcs);
                        bp = new List<bx_pl>(traversalPolylineFuses);
                        sbtxt = new List<MText>(traversalPumpTexts);
                        clbx = "未找到保险";
                        lj = new List<int>();
                        //File.AppendAllText("x.txt", "\n初始出点" + i.StartPoint.X + " " + i.StartPoint.Y + "\r\n");                     
                        bre = false;
                        //在这里添加对et_line的判断，对bxsjs进行初始化
                        //如果et_line的起点靠近包含蓄电池的文本就初始化bxsjs为0，否则初始化为1

                        if (i.StartPoint.Y > battery_point_Y && Math.Abs(i.StartPoint.X - battery_point_X) < 40)
                        {
                            bxsjs = 0;
                        }
                        else
                        {
                            bxsjs = 1;
                        }
                        otpt = new List<Point3d>();
                        otpt.Add(i.EndPoint);

                        while (true)
                        {
                            bre = false;
                            getbxs();       // 矩形保险丝
                            getcirbxs();    // 圆弧型保险丝
                            getli();        // 寻线
                            getarc();       // 寻圆弧
                            getjdq();       // 寻径继电器
                            getqj();        // 负载
                            getsb();        // 水泵
                            getzdq();       // 制动开关
                            if (!bre)
                                break;
                        }

                    }
                    #endregion
                }
            }
        }



        private StreamReader _rstream = null;
        private StreamWriter _wstream = null;

        private void WriteLstToTxt(ListBox lst, string spath)  //listbox 写入txt文件
        {
            _wstream = new StreamWriter(spath = "@E://zhongqituzhi//新建文本文档.txt");
            int count = lst.Items.Count;
            _wstream = new StreamWriter(spath);
            for (int i = 0; i < count; i++)
            {
                string data = lst.Items[i].ToString();
                _wstream.Write(data);
                _wstream.WriteLine();
            }
            _wstream.Flush();
            _wstream.Close();
        }
        private void ReadTxtToLst(ListBox lst, string spath)  //listbox 读取txt文件
        {
            _rstream = new StreamReader(spath = "@E://zhongqituzhi//新建文本文档.txt", System.Text.Encoding.Default);
            _rstream = new StreamReader(spath, System.Text.Encoding.Default);
            string line;
            while ((line = _rstream.ReadLine()) != null)
            {
                lst.Items.Add(line);
            }
            _rstream.Close();
        }



        public static List<Form1.gpipei> lpipei = new List<Form1.gpipei>();

        private void toolStripButton11_Click(object sender, EventArgs e)
        {
            using (Form1 f1 = new Form1())
            {
                string spath = "未匹配.txt";
                //_rstream = new StreamReader(spath = "新建文本文档.txt", System.Text.Encoding.Default);
                _rstream = new StreamReader(spath, System.Text.Encoding.Default);
                //listBox1.
                string line;
                //(line = _rstream.ReadLine())
                while ((line = _rstream.ReadLine()) != null)
                {

                    f1.checkedListBox1.Items.Add(line);
                }

                _rstream.Close();
                if (f1.ShowDialog() == DialogResult.OK)
                {

                }

            }
            
        }
        public void teee1(object sender, EventArgs e)
        {
            using (Form2 f2 = new Form2())
            {

                if (f2.ShowDialog() == DialogResult.OK)
                {

                }
                sq = f2.UserName;
                MessageBox.Show(sq);
            }
        }

        public void toolStripButton11_Click_1(object sender, EventArgs e)
        {
            string selectedOption = ShowCustomDialog();
            gselect.select = selectedOption;
            // 根据用户的选择结果进行相应操作
            switch (selectedOption)
            {
                case "线束图":
                    {
                        MessageBox.Show("用户选择了线束图", "选择结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (Global.mlist.Count() == 4)
                        {
                            MessageBox.Show("您已经选择过两个点了，请重新选择", "提示");
                            Global.mlist.Clear();
                        }
                        MessageBox.Show("请选择图纸识别范围的边界点（即左上和右下）用于优化结果。", "提示");
                        this.basepanel.MouseClick += Basepanel_MouseClick2;
                        break;
                    }
                case "电器图":
                    {
                        MessageBox.Show("用户选择了电器图", "选择结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (Global.mlist.Count() == 4)
                        {
                            MessageBox.Show("您已经选择过两个点了，请重新选择", "提示");
                            Global.mlist.Clear();
                        }
                        MessageBox.Show("请选择图纸识别范围的边界点（即左上和右下）用于优化结果。", "提示");
                        this.basepanel.MouseClick += Basepanel_MouseClick_ele;
                        break;
                    }
                default:
                    MessageBox.Show("无效的选择", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }

        public void Insertxianshu(int X, int Y, string textContent)
        {
            using (Transaction trans = database.TransactionManager.StartTransaction())
            {
                try
                {
                    // 创建一个点，表示文本插入的位置
                    Point3d insertionPoint = new Point3d(X, Y, 0);

                    // 创建文本对象
                    DBText dbText = new DBText
                    {
                        Position = insertionPoint,
                        TextString = textContent,
                        Height = 10, // 设置文本高度
                        Color = Teigha.Colors.Color.FromRgb(colorAll.R, colorAll.G, colorAll.B)
                    };

                    // 将文本对象添加到模型空间
                    BlockTable blockTable = trans.GetObject(database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    modelSpace.AppendEntity(dbText);
                    trans.AddNewlyCreatedDBObject(dbText, true);

                    // 提交事务
                    trans.Commit();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Exception occurred: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    trans.Abort();
                }
            }

            // 更新视图
            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                pView.Invalidate();
            }
        }
        // 选择记录类
        private class SelectionRecord
        {
            public string Fuse { get; set; }
            public string LoadName { get; set; }
            public string PinName { get; set; }

            public override string ToString()
            {
                return $"保险丝: {Fuse}, 负载名称: {LoadName}, 引脚名称: {PinName}";
            }
        }
        // 定义选择步骤的枚举
        private enum SelectionStep
        {
            SelectFuse,
            SelectLoadName,
            SelectPinName,
            None
        }

        // 用于跟踪当前选择步骤
        private SelectionStep currentStep = SelectionStep.None;

        // 用于存储当前的选择记录
        private SelectionRecord currentRecord = null;

        // 用于存储所有的选择记录
        private List<SelectionRecord> selectionRecords = new List<SelectionRecord>();

        public void TableProces(object sender, EventArgs e)
        {
            // 清空之前的选择记录
            selectionRecords.Clear();

            // 初始化新的选择记录
            currentRecord = new SelectionRecord();

            // 设置当前步骤为选择保险丝
            currentStep = SelectionStep.SelectFuse;

            // 提示用户选择保险丝
            MessageBox.Show("请点击选择一个保险丝的文本。", "选择保险丝", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 绑定鼠标点击事件处理器
            this.basepanel.MouseDown += Basepanel_MouseDown_Handler;
        }
        private void Basepanel_MouseDown_Handler(object sender, MouseEventArgs e)
        {
            // 获取鼠标点击位置
            Point mouseDownPt = new Point(e.X, e.Y);

            using (Teigha.GraphicsSystem.View pView = helperDevice.ActiveView)
            {
                ObjectIdCollection selectNew = new ObjectIdCollection();

                // 定义选择窗口（这里以点击点为中心的一个小矩形）
                pView.Select(
                    new Point2dCollection(new Point2d[]
                    {
                new Point2d(mouseDownPt.X + 5, mouseDownPt.Y + 5),
                new Point2d(mouseDownPt.X - 5, mouseDownPt.Y - 5)
                    }),
                    new SRnew(selectNew, database.CurrentSpaceId),
                    Teigha.GraphicsSystem.SelectionMode.Crossing
                );

                if (selectNew != null && selectNew.Count > 0)
                {
                    foreach (ObjectId item in selectNew)
                    {
                        if (!item.IsNull && !item.IsErased)
                        {
                            using (Entity ent = (Entity)item.GetObject(OpenMode.ForRead))
                            {
                                // 高亮显示被选择的实体
                                ent.Highlight();
                                GlobaltextData3.SelectedEntityId = ent.ObjectId;
                                GlobaltextData3.SelectedEntity = ent;

                                if (GlobaltextData3.SelectedEntity != null)
                                {
                                    Entity ent1 = GlobaltextData3.SelectedEntity;
                                    if (ent1 is MText mtext)
                                    {
                                        string selectedText = mtext.Text;
                                        HandleSelectionStep(selectedText);
                                    }
                                    else
                                    {
                                        MessageBox.Show("选择有误，请选择MText对象。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }

                                    // 取消绑定事件处理器以避免重复处理
                                    this.basepanel.MouseDown -= Basepanel_MouseDown_Handler;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void HandleSelectionStep(string selectedText)
        {
            switch (currentStep)
            {
                case SelectionStep.SelectFuse:
                    currentRecord.Fuse = selectedText;
                    MessageBox.Show($"已选择保险丝: {selectedText}", "选择成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // 进入下一个步骤：选择负载名称
                    currentStep = SelectionStep.SelectLoadName;
                    PromptNextStep("负载名称");
                    break;

                case SelectionStep.SelectLoadName:
                    currentRecord.LoadName = selectedText;
                    MessageBox.Show($"已选择负载名称: {selectedText}", "选择成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // 进入下一个步骤：选择引脚名称
                    currentStep = SelectionStep.SelectPinName;
                    PromptNextStep("引脚名称");
                    break;

                case SelectionStep.SelectPinName:
                    currentRecord.PinName = selectedText;
                    MessageBox.Show($"已选择引脚名称: {selectedText}", "选择成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // 将当前记录添加到列表中
                    selectionRecords.Add(new SelectionRecord
                    {
                        Fuse = currentRecord.Fuse,
                        LoadName = currentRecord.LoadName,
                        PinName = currentRecord.PinName
                    });

                    // 询问用户是否继续添加
                    DialogResult result = MessageBox.Show("是否继续添加下一组选择？", "继续", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        // 重置当前记录并进入选择保险丝步骤
                        currentRecord = new SelectionRecord();
                        currentStep = SelectionStep.SelectFuse;
                        PromptNextStep("保险丝");
                    }
                    else
                    {
                        // 结束选择流程
                        currentStep = SelectionStep.None;
                        currentRecord = null;
                        MessageBox.Show("选择流程已结束。", "结束", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // 可选：在这里处理所有选择记录，例如显示、保存等
                        DisplaySelections();
                    }
                    break;

                default:
                    break;
            }
        }
        private void PromptNextStep(string stepName)
        {
            MessageBox.Show($"请点击选择一个{stepName}的文本。", $"选择{stepName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // 重新绑定鼠标点击事件处理器以等待用户的选择
            this.basepanel.MouseDown += Basepanel_MouseDown_Handler;
        }
        private int GetNextInsuranceLevel(List<string> lines, Regex insuranceRegex)
        {
            int maxLevel = 0;
            foreach (var line in lines)
            {
                Match match = insuranceRegex.Match(line);
                if (match.Success)
                {
                    string fullMatch = match.Groups[0].Value;
                    Match levelMatch = Regex.Match(fullMatch, @"第(\d+)级保险");
                    if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out int level))
                    {
                        if (level > maxLevel)
                        {
                            maxLevel = level;
                        }
                    }
                }
            }
            return maxLevel + 1;
        }
        private void DisplaySelections()
        {
            if (selectionRecords.Count == 0)
            {
                MessageBox.Show("没有任何选择记录。", "信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 构建所有选择记录的字符串表示
            string allSelections = string.Join(Environment.NewLine, selectionRecords.Select(r => r.ToString()));
            MessageBox.Show(allSelections, "所有选择记录", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 更新文件 x.txt
            // 读取文件内容，如果文件不存在，则初始化为空列表
            List<string> lines;
            if (File.Exists("x.txt"))
            {
                lines = File.ReadAllLines("x.txt", Encoding.UTF8).ToList();
            }
            else
            {
                lines = new List<string>();
            }

            // 使用正则表达式匹配“第X级保险FuseName”行，其中X为任意数字
            Regex insuranceRegex = new Regex(@"^第\d+级保险(.+)$");

            // 使用 HashSet 跟踪已处理的保险丝名称
            HashSet<string> processedFuses = new HashSet<string>();

            foreach (var record in selectionRecords)
            {
                string fuseName = record.Fuse;
                int fuseLineIndex = -1;

                if (!processedFuses.Contains(fuseName))
                {
                    // 第一次处理该保险丝，尝试查找并更新或添加新的块
                    // 查找匹配的保险丝行
                    for (int i = 0; i < lines.Count; i++)
                    {
                        Match match = insuranceRegex.Match(lines[i]);
                        if (match.Success)
                        {
                            string fuseInFile = match.Groups[1].Value.Trim();
                            if (fuseInFile.Equals(fuseName, StringComparison.OrdinalIgnoreCase))
                            {
                                fuseLineIndex = i;
                                break;
                            }
                        }
                    }

                    if (fuseLineIndex != -1)
                    {
                        // 找到对应的保险丝块
                        // 确保“对应保险丝”行存在并正确
                        int correspondingFuseLineIndex = fuseLineIndex + 1;
                        if (correspondingFuseLineIndex >= lines.Count ||
                            !lines[correspondingFuseLineIndex].StartsWith("对应保险丝") ||
                            !lines[correspondingFuseLineIndex].Contains(fuseName))
                        {
                            // 如果“对应保险丝”行不正确，插入或替换
                            if (correspondingFuseLineIndex < lines.Count && lines[correspondingFuseLineIndex].StartsWith("对应保险丝"))
                            {
                                lines[correspondingFuseLineIndex] = $"对应保险丝{fuseName}";
                            }
                            else
                            {
                                // 找到当前块的结束位置（下一个保险丝块或文件结尾）
                                int insertPos = fuseLineIndex + 1;
                                while (insertPos < lines.Count && !insuranceRegex.IsMatch(lines[insertPos]))
                                {
                                    insertPos++;
                                }
                                lines.Insert(insertPos, $"对应保险丝{fuseName}");
                                correspondingFuseLineIndex = insertPos;
                            }
                        }

                        // 查找“负载”和“引脚”行
                        int loadLineIndex = -1;
                        int pinLineIndex = -1;

                        // 从对应保险丝行之后开始查找，直到下一个保险丝块
                        for (int i = correspondingFuseLineIndex + 1; i < lines.Count; i++)
                        {
                            if (insuranceRegex.IsMatch(lines[i]))
                            {
                                break; // 到下一个保险丝块
                            }
                            if (lines[i].StartsWith("负载"))
                            {
                                loadLineIndex = i;
                            }
                            if (lines[i].StartsWith("引脚"))
                            {
                                pinLineIndex = i;
                            }
                        }

                        // 更新或添加“负载”行
                        if (loadLineIndex != -1)
                        {
                            lines[loadLineIndex] = $"负载{record.LoadName}";
                        }
                        else
                        {
                            // 在对应保险丝行之后添加“负载”行
                            int insertIndex = correspondingFuseLineIndex + 1;
                            lines.Insert(insertIndex, $"负载{record.LoadName}");
                            // 如果“引脚”行存在，调整其索引
                            if (pinLineIndex != -1 && pinLineIndex > correspondingFuseLineIndex)
                            {
                                pinLineIndex = insertIndex + 1;
                            }
                        }

                        // 更新或添加“引脚”行
                        if (pinLineIndex != -1)
                        {
                            lines[pinLineIndex] = $"引脚{record.PinName}";
                        }
                        else
                        {
                            // 找到最后一个相关行后插入“引脚”行
                            int insertIndex = correspondingFuseLineIndex + 2; // 默认在“负载”后面
                            if (loadLineIndex != -1)
                            {
                                insertIndex = loadLineIndex + 1;
                            }
                            lines.Insert(insertIndex, $"引脚{record.PinName}");
                        }
                    }
                    else
                    {
                        // 未找到对应的保险丝块，添加新的块到文件末尾
                        // 动态确定级别：获取文件中存在的最大级别并加1
                        //int newLevel = GetNextInsuranceLevel(lines, insuranceRegex);
                        int newLevel = 2;
                        lines.Add($"第{newLevel}级保险{record.Fuse}");
                        lines.Add($"对应保险丝{record.Fuse}");
                        lines.Add($"负载{record.LoadName}");
                        lines.Add($"引脚{record.PinName}");
                    }

                    // 标记该保险丝已处理
                    processedFuses.Add(fuseName);
                }
                else
                {
                    // 该保险丝已在前面处理过，直接在末尾添加新的块
                    int newLevel = 2;
                    lines.Add($"第{newLevel}级保险{record.Fuse}");
                    lines.Add($"对应保险丝{record.Fuse}");
                    lines.Add($"负载{record.LoadName}");
                    lines.Add($"引脚{record.PinName}");
                }
                // 写回文件
                File.WriteAllLines("x.txt", lines, Encoding.UTF8);
                //MessageBox.Show("文件已成功更新。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

    }

}
