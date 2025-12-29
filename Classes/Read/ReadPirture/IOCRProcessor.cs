using System.Collections.Generic;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public interface IOCRProcessor
    {
        bool IsReady();
        List<string> ProcessImage(string imagePath);
    }
}