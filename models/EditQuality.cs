namespace OpenAnalytics.models;

internal sealed class EditQuality
{
    public int Edits { get; private set; }
    private int WithError { get; set; }
    private int Clean => Edits - WithError;
    public double CleanRate => Edits == 0 ? 0 : (double)Clean / Edits * 100;

    public void Record(bool leftError)
    {
        Edits++;
        if (leftError)
        {
            WithError++;
        }
    }
}
