﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// Width of output drawing
        /// </summary>
        // ****Ancho de dibujo de salida
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        // ****Alto de dibujo de salida
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        // ****Grosor de puntos de detección
        private const double JointThickness = 5;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        // ****Grosor de lineas de borde de pantalla (lineas rojas)
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        // ****Punto central de detección del cuerpo
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        // ****color de los puntos de deteccion detectados
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>     
        // ****color de los puntos de deteccion intuidos
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        // ****Grosor y color de huesos detectados de esqueleto
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>   
        // ****Grosor y color de huesos intuidos de esqueleto
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        // ****Activación de kinect
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        // ****Grupo de dibujo del esqueleto
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        // ****Dibujo de la imagen que se mostrará
        private DrawingImage imageSource;


        //----------------------------------
        //--------variables añadidas--------
        //----------------------------------

        /// <summary>
        /// Para reservar pixels de color
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// mapa de bit de color
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Grosor y color de "hueso" cuello-cabeza
        /// </summary>
        private Pen trackedBonePenHead = new Pen(Brushes.Green, 6);

        /// <summary>
        /// valdrá true si comenzó el movimiento de cabeza, false en caso contrario
        /// </summary>
        private bool movIniciado = false;

        /// <summary>
        /// valdrá true cuando se complete el movimiento de la cabeza hacia la derecha
        /// </summary>
        private bool movDerechaIniciado = false;

        /// <summary>
        /// valdrá true cuando se complete el movimiento de la cabeza hacia la izquierda
        /// </summary>
        private bool movIzquierdaIniciado = false;

        /// <summary>
        /// valdrá true cuando el movimiento de cabeza se realice correctamente
        /// </summary>
        private bool movFinalizado = false;

        ///<summary>
        /// número de frames por segundo
        /// </summary>
        private readonly int FPS = 30;

        ///<summary>
        /// contador de frames
        /// </summary>
        private int contFPS = 0;

        ///<summary>
        /// Espera inicial para que el usuario se coloque en posición
        /// </summary>
        private bool esperaInicial = true;

        ///<summary>
        /// grado de inclinación
        /// </summary>
        private int gradoAnterior = 0, gradoActual = 0, gradoPosErguido = 0, 
            gradoActualZ = 0, gradoPosErguidoZ = 0, gradoDif = 0, gradoDifZ = 0;


//--------------------------------------------------------------------------------------------------
//------------------------------------------------METODOS-------------------------------------------
//--------------------------------------------------------------------------------------------------
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        // ****Inicialización de los componentes de la clase
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        // ****Bordes en pantalla de continuidad del esqueleto (desactivada su llamada por decisión propia)
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        // ***Ejecución de tareas iniciales
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            
            // *** Inicializar el sensor
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (this.sensor != null)
            {
                // En ImageE se proyectará el esqueleto y en ImageC la imagen en color del senson kinect
                // Display the drawing using our image control
                ImageE.Source = this.imageSource;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // color stream
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Espacio reservado para pixels de color
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                //Bitmaps para mostrar en pantalla
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, 
                    this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Para que en el objeto Image se proyecte la imagen
                this.ImageC.Source = this.colorBitmap;

                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }

        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        // ****Ejecución de tareas de apagado
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }


        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        //*** Para poder ver la imagen capturada por la camara kinect
        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        // ****Controlador de eventos del esqueleto
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                //Es necesario que se declare el "color" como transparente para poder ver el esqueleto y la imagen del cuerpo
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                      
                        //RenderClippedEdges(skel, dc);  //Desactivada la llamada a esta función por decisión propia

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            if(!movFinalizado)
                                this.movimientoCuello(skel); //llamada al metodo para detectar el movimiento del cuello
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                           
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }


        /// <summary>
        /// Metodo que detecta el movimiento de cuello del usuario
        /// </summary>
        /// <param name="skeleton">objeto esqueleto</param>
        private void movimientoCuello(Skeleton skeleton)
        {
            //Colores utilizados:
                //Marcar posiciones (cabeza en centro, cabeza en derecha) ---> color agua
                //movimiento en curso ---> color azul
                //movimiento erroneo ---> rojo
                //movimiento finalizado ---> verde

            if (!esperaInicial) //una vez que la espera inicial (2 segundo) se cumpla
            {
                Joint cabeza = skeleton.Joints[JointType.Head];
                Joint centroPecho = skeleton.Joints[JointType.ShoulderCenter];

                if(contFPS == 0){
                    gradoAnterior = gradoActual;
                    gradoPosErguido = gradoInclinacion(centroPecho.Position.X, centroPecho.Position.Y, centroPecho.Position.X, cabeza.Position.Y);
                    gradoPosErguidoZ = gradoInclinacion(centroPecho.Position.Z, centroPecho.Position.Y, centroPecho.Position.Z, cabeza.Position.Y);
                    gradoActual = gradoInclinacion(centroPecho.Position.X, centroPecho.Position.Y, cabeza.Position.X, cabeza.Position.Y);
                    gradoActualZ = gradoInclinacion(centroPecho.Position.Z, centroPecho.Position.Y, cabeza.Position.Z, cabeza.Position.Y);
                    gradoDif = gradoPosErguido - gradoActual;
                    gradoDifZ = System.Math.Abs(gradoPosErguidoZ - (gradoActualZ+10)); //se suma 10 para regular el error de kinect en la detección
                }
                contFPS++;
                if(contFPS == 10)
                    contFPS = 0;

                    
                //si no se ha iniciado el movimiento y el usuario esta con la cabeza recta se puede iniciar el movimiento
                if (!movIniciado && gradoDif < 2 && gradoDifZ <= 15)
                {
                    movIniciado = true;
                    movDerechaIniciado = true;
                    this.trackedBonePenHead.Brush = Brushes.Aqua;
                    lblRepetir.Visibility = Visibility.Hidden;

                }
                //movimiento incorrecto (cabeza hacia adelante o hacia atrás) se avisa con el color rojo y reiniciando el movimiento
                else if (movIniciado && (gradoDifZ > 15))
                {
                    this.trackedBonePenHead.Brush = Brushes.Red;
                    txtGrados.Text = "";
                    txtZ.Text = "";
                    movIniciado = movDerechaIniciado = movIzquierdaIniciado = false;
                    lblRepetir.Visibility = Visibility.Visible;
                }
                //cuando se alcanza el maximo del movimiento a la derecha
                else if (movDerechaIniciado && gradoDif >= 25)
                {
                    movIzquierdaIniciado = true;
                    movDerechaIniciado = false;
                    this.trackedBonePenHead.Brush = Brushes.Aqua;
                }
                //entra cuando el usuario esta moviendo el cuello hacia la derecha
                else if (movDerechaIniciado && gradoDif < 25 && gradoDif > 3)
                {
                    this.trackedBonePenHead.Brush = Brushes.Blue;
                }
                //cuando se alcanza el maximo del movimiento a la izquierda (finaliza el ejercicio)
                else if (movIzquierdaIniciado && gradoDif <= -25)
                {
                    movIzquierdaIniciado = false;
                    movFinalizado = true;
                    movIniciado = false;
                    this.trackedBonePenHead.Brush = Brushes.Green;
                    txtGrados.Text = "";
                    txtZ.Text = "";
                    lblFin.Visibility = Visibility.Visible;
                }
                ////entra cuando el usuario esta moviendo el cuello hacia la izquierda, 
                else if (movIzquierdaIniciado && gradoDif > -25 && gradoDif < 22)
                {
                    this.trackedBonePenHead.Brush = Brushes.Blue;
                }

                if (movIniciado && contFPS == 0)//escribir en los textBox los grados
                {
                    txtGrados.Text = System.Convert.ToString(gradoDif);
                    txtZ.Text = System.Convert.ToString(gradoDifZ);
                }
            }

            else //al inicio se activa una espera inicial de 1 segundos para que el usuario se recoloque
            {
                contFPS++;
                if (contFPS == FPS)
                {
                    esperaInicial = false;
                    contFPS = 0; //para cuando vuelva a usarse para calcular los grados de inclinación
                    lblInicio.Visibility = Visibility.Hidden;
                }
            }
        }

        /// <summary>
        /// Metodo que devuelve el grado de inclinación de una recta.
        /// Entran por parametros las coordenadas de la recta.
        /// </summary>
        private int gradoInclinacion(float x1, float y1, float x2, float y2)
        {
            float grado = 0;
            float m = (y2 - y1) / (x2 - x1);
            grado = (float)(System.Math.Atan(m) * (180 / System.Math.PI));
            if (grado < 0)
                grado = 180 + grado;     

            return (int)grado;
        }
        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        // ***Dibujar los huesos y articulaciones del esqueleto
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);



            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        // ****Dibujar una linea de hueso entre dos articulaciones
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                //el "hueso" cabeza-hombro lo pintará del color deseado según si el movimiento es correcto o no
                if (joint0 == skeleton.Joints[JointType.Head]) 
                    drawPen = this.trackedBonePenHead;
                else
                    drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        // ****Activar o desactivar el modo sentado
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        /// <summary>
        /// Método click de botón "Repetir" el cual reinicia las variables que tiene que ver
        /// con el movimiento de la cabeza para poder repetir el ejercicio sin tener que 
        /// reiniciar el programa
        /// </summary>
        private void btnRepetir_Click(object sender, RoutedEventArgs e)
        {
            contFPS = 0;
            movFinalizado = false;
            lblFin.Visibility = Visibility.Hidden;
            lblInicio.Visibility = Visibility.Visible;
            esperaInicial = true;
        }
    }
}