using practica.Entity;
using System.Xml.Serialization;

namespace practica.Config
{
    public class UsersConfiguration
    {



        private List<User> users;

        private string XmlFileName;
        public UsersConfiguration(string _pathToXML)
        {
            XmlFileName = _pathToXML;

            users = GetUsersFromXML();

        }
        public bool isUserConfigurationLoaded {
            get{
                return users != null && users.Count > 0;
            }
        }
        public int UserCount {
            get {
                return users != null ? users.Count : 0;
            }
        }   



        public bool CheckNumber(string number) {
            foreach (User user in users)
            {
                if (user.Number == number)
                {
                    if (!string.IsNullOrEmpty(user.Number)) { 
                        Console.WriteLine(user.Name);
                        return true;
                    }
                }
            }
            return false;
        }   

        public string GetUserMacAdress(string number) {

            if (users != null && users.Count > 0) { 
                User found =  users.Find(usr => usr.Number == number);
                if (found != null)
                {
                    if (!string.IsNullOrEmpty(found.MAC)) { 
                      
                        return found.MAC;
                    }
                }
           
            }

            return string.Empty;
        }


       
        public  List<User> GetUsersFromXML()
        {
            XmlSerializer reader = new XmlSerializer(typeof(List<User>));
            try
            {
                using (FileStream fs = new FileStream(XmlFileName, FileMode.Open))
                {

                    List<User> UserList = (List<User>)reader.Deserialize(fs);
                    
                    if (UserList != null && UserList.Count > 0 )
                    {
                        Console.WriteLine($"Successfully loaded {UserList.Count} users from {XmlFileName}");
                        return UserList;
                    }
                    else
                    {
                        throw new Exception($"No users found in {XmlFileName}");
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"File {XmlFileName} not found: " + ex.Message);
                XmlSerializer writer = new XmlSerializer(typeof(List<User>));

                using (FileStream fs = new FileStream(XmlFileName, FileMode.Create)) {

                    User usr = new User() { Name = "Default User", Number = "000000000", MAC = "00:00:00:00:00:00" };
                    writer.Serialize(fs, new List<User> { usr });

                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access to file {XmlFileName} denied: " + ex.Message);
            }
            Console.WriteLine($"Error loading users from {XmlFileName}. Returning empty user list.");
            return new List<User>();
        }








    }
};
