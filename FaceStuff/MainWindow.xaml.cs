using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System.IO;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
namespace FaceStuff
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string FACELISTID = "inseyabtestfaces";
        IFaceServiceClient faceServiceClient;
        string newImgSrc;
        Guid newFaceID;

        //Connect to FaceList inseyabtestfaces
        public MainWindow()
        {
            InitializeComponent();
            faceServiceClient = new FaceServiceClient("16306c6a4b53406da6eca3fb9e42dedb");
            Sync();
            //endif
        }

        private async void Sync()
        {
            /* Get Face List
             * Compare Number of elements in FaceList with number of records
             * If number of records are equal to number of elements 
             *        then return, as both are in sync
             * Else If number of records are greater
             *        then delete the records corresponding to the unknown faces
             * Else If number of elements are greater 
             *          then delete the facelist item corresponding to the non-existing record
             *  */

            FaceList Emps = await faceServiceClient.GetFaceListAsync(FACELISTID);
            

            DataTable emprecs = ExecuteQuery("Select * from Person");
            if (emprecs.Rows.Count == Emps.PersistedFaces.Count())
            {
                MessageBox.Show("Both records are equal");
            }
            else if(emprecs.Rows.Count > Emps.PersistedFaces.Count())
            {
                MessageBox.Show("There are more database entries!");

            }
            else if(emprecs.Rows.Count < Emps.PersistedFaces.Count())
            {
                MessageBox.Show("There are more Facelist items!");
            }
        }

        /*public async void faceNotExist()
        {
            var data = ExecuteQuery("select PersonID, ImageURL from Person where FaceID = '0'");
            if(data.Rows.Count > 0)
            {
                Face[] newFace = new Face[data.Rows.Count];
                string url = " ";
                int i = 0;
                foreach(DataRow d in data.Rows)
                {
                    url = d.ItemArray[1].ToString();
                    newFace[i] = await UploadAndDetectFaces(url);
                    ExecutePUTQuery("UPDATE Person SET FaceID = '"+newFace[i].FaceId+"' WHERE PersonID = '"+d.ItemArray[0].ToString()+"'");
                    i++;
                }
            }
        }
        */
      public int ExecutePUTQuery(string query)
      {
          string sdwConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Database=FaceAPIdb;Trusted_Connection=yes;Encrypt=False;TrustServerCertificate=True;";
            var res = 0;
            // Create a SqlCommand object and pass the constructor the connection string and the query string.
            // SqlCommand queryCommand = new SqlCommand(query, new SqlConnection(sdwConnectionString));
            try
            {
                using (SqlConnection connection = new SqlConnection(sdwConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        connection.Open();
                        res = cmd.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                return res;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error occured writing new row to database", MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
      }


        public DataTable ExecuteQuery(string query)
        {
            string sdwConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Database=FaceAPIdb;Trusted_Connection=yes;Encrypt=False;TrustServerCertificate=True;";

            DataTable dataTable = new DataTable();
            // Create a SqlCommand object and pass the constructor the connection string and the query string.
            // SqlCommand queryCommand = new SqlCommand(query, new SqlConnection(sdwConnectionString));
            using (SqlConnection connection = new SqlConnection(sdwConnectionString))
            {
                using (SqlDataAdapter mydata = new SqlDataAdapter(query, connection))
                {
                    
                    mydata.Fill(dataTable);
                }
            }
            return dataTable;
        }
   

        private void GetImage_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            if (!(bool)result)
            {
                return;
            }

            string filePath = openDlg.FileName;
            newImgSrc = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FaceImage.Source = bitmapSource;

            //Enable Verify button
            Verify.IsEnabled = true;
            reset.IsEnabled = true;
        }

        private async void Verifybtn_Click(object sender, RoutedEventArgs e)
        {
            Verify.IsEnabled = false;
            GetImage.IsEnabled = false;
            reset.IsEnabled = false;
            /*
             * First, get face rect, faceid of face currently uploaded to application
             * Send FaceID to ProjectOxford inseyabtestfaces face list with FindSimilar API
             * If result comes with >75% confidence, find matching faceID in database and display
             * else go to create new
             */

            //Get Face ID
            string url = FaceImage.Source.ToString();
            Face tempFace = await UploadAndDetectFaces(newImgSrc);

            //store faceid in field to be used later
            newFaceID = tempFace.FaceId;

            if (tempFace != null)
            {
                //call function and store result in matchingFace
                SimilarPersistedFace matchingFace = await FindMatchingFace(tempFace.FaceId);

                if (matchingFace.Confidence >= 0.30)
                {
                    try
                    {

                        //Query Database for matching record
                        DataTable empStuff = ExecuteQuery("Select PersonID, FirstName, LastName, CNIC, FaceID from Person where FaceID = '" + matchingFace.PersistedFaceId.ToString() + "'");
                        DataTable empImgdata = ExecuteQuery("Select FaceID, ImageURL from Person where FaceID = '" + matchingFace.PersistedFaceId.ToString() + "'");

                        string urlz=empImgdata.Rows[0]["ImageURL"].ToString();
                        //populate datagrid
                        dataGrid.ItemsSource = empStuff.DefaultView;
                        //Populate Image
                        BitmapImage empImg = new BitmapImage();
                        empImg.BeginInit();
                        empImg.UriSource = new Uri(urlz);
                        empImg.EndInit();
                        empimg.Source = empImg;
                        reset.IsEnabled = true;

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Unable to query matching records", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }
                else
                {
                    //enable create fields and focus there
                    MessageBox.Show("No faces available.");
                    fName.IsEnabled = true;
                    lName.IsEnabled = true;
                    cnic.IsEnabled = true;
                    submitform.IsEnabled = true;
                    reset.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show("No faces found");
                fName.IsEnabled = true;
                lName.IsEnabled = true;
                cnic.IsEnabled = true;
                submitform.IsEnabled = true;
                reset.IsEnabled = true;
            }
        }
        private async Task<Face> UploadAndDetectFaces(string imageFilePath)
        {
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    //faceServiceClient
                    Face[] faces = await faceServiceClient.DetectAsync(imageFileStream);
                    return faces[0];
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Upload and detect face failed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return null;
            }
        }

        private async Task<SimilarPersistedFace> FindMatchingFace(Guid fid)
        {
            try
            {
                SimilarPersistedFace[] faces = await faceServiceClient.FindSimilarAsync(fid, FACELISTID, 1);
                return faces[0];
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Could not find matching faces", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return new SimilarPersistedFace();
            }
        }

        private void reset_Click(object sender, RoutedEventArgs e)
        {
            Verify.IsEnabled = true;
            GetImage.IsEnabled = true;
            FaceImage.Source = null;

            dataGrid.ItemsSource = null;

            fName.Text = null;
            fName.IsEnabled = false;
            lName.Text = null;
            lName.IsEnabled = false;
            cnic.Text = null;
            cnic.IsEnabled = false;
            submitform.IsEnabled = false;
            empimg.Source = null;
        }

        private async void submitform_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //get info from text fields
                string FirstName = fName.Text;
                string LastName = lName.Text;
                string CNIC = cnic.Text;
                string picture = newImgSrc;

                //New Persisted face id
                Guid newPersistedFaceID = new Guid();

                //add faceID to FaceList and get result (new persisted face id)
                using (Stream imageStream = File.OpenRead(picture))
                {
                    var addResult = await faceServiceClient.AddFaceToFaceListAsync(FACELISTID, imageStream, "Name: " + FirstName + "_" + LastName);
                    //store new persisted face id
                    newPersistedFaceID = addResult.PersistedFaceId;
                }
                int res = ExecutePUTQuery("Insert into Person (FirstName, LastName, CNIC, FaceID, ImageURL) VALUES ('" + FirstName + "', '" + LastName + "', '" + CNIC + "', '" + newPersistedFaceID.ToString() + "', '" + picture + "')");
                //If Insert query terminates in error, then delete the FaceList entry to maintain consistency
                if(res <= 0)
                {
                    await faceServiceClient.DeleteFaceFromFaceListAsync(FACELISTID, newPersistedFaceID);
                }
                else
                {
                    MessageBox.Show("Success! " + res + " rows added");
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Message type: "+ex.Message+", \n"+"Message: ", "Error Filling data", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }
    }

}
