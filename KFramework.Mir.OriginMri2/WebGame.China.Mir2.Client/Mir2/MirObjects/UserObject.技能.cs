namespace Client.MirObjects
{
    public partial class UserObject : PlayerObject
    {
        public ClientMagic GetMagic(Spell spell)
        {
            for (int i = 0; i < Magics.Count; i++)
            {
                ClientMagic magic = Magics[i];
                if (magic.Spell != spell) continue;
                return magic;
            }

            return null;
        }



        public void ClearMagic()
        {
            NextMagic = null;
            NextMagicDirection = 0;
            NextMagicLocation = Point.Zero;
        } 

    }
}