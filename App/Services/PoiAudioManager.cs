public class PoiAudioManager
{
    int lastPoiId = -1;
    DateTime lastPlay = DateTime.MinValue;

    const int COOLDOWN = 300; // 5 phút

    public bool CanPlay(int poiId)
    {
        if (poiId == lastPoiId)
        {
            if ((DateTime.Now - lastPlay).TotalSeconds < COOLDOWN)
                return false;
        }

        lastPoiId = poiId;
        lastPlay = DateTime.Now;

        return true;
    }
}