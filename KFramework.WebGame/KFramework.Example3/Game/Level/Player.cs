using System;
using System.Collections.Generic;

namespace KFramework.Example3
{
    internal class Player : TileBase
    {
        public enum EPlayerModeScripting
        {
            None = 0,
            StartGame = 1,
            GameWin = 2,
        }

        public enum EPlayerState
        {
            Appearing = 1, //出生动画
            Normal = 2,  //正常
            Blink = 3, //闪烁 无敌状态
            Deading = 4, //在播放死亡动画
            Dead = 5, //完全死亡
        }

        public enum EPlayerType
        {
            Player = 1,
            UltimatePlayer = 2,
            BigPlayer = 4,
            FireBigPlayer = 8,
            UltimateBigPlayer = 16,
        }

        public enum EAniType:byte
        {
            Idle = 1,
            Walk = 2,
            Jump = 4,
            Die = 8,
            ClimbPole = 16,   //爬干
            Crouch = 32,      //蹲伏
            HugPole = 64,     //抱柱
        }

        public Level mLevel;

        public SoundEffect jumpSound;
        public SoundEffect jumpBigSound;
        public SoundEffect flagPoleSound;
        public SoundEffect pipeSound;
        public SoundEffect dieSound;
        public SoundEffect oneUpSound;
        public SoundEffect turnBigSound;
        public SoundEffect coinSound;
        public SoundEffect kickSound;
        public SoundEffect endGameSound;
        public SoundEffect fireballSound;

        //小玛丽 没有蹲伏，只有 大 玛丽 才有
        private Animation PlayerIdle;
        private Animation PlayerWalk;
        private Animation PlayerJump;
        private Animation PlayerDie;
        private Animation PlayerClimbPole; //爬干
        private Animation PlayerHugPole; //抱柱

        private Animation PlayerUltimateIdle;
        private Animation PlayerUltimateWalk;
        private Animation PlayerUltimateRun;
        private Animation PlayerUltimateJump;

        // 大玛丽
        private Animation PlayerBigIdle;
        private Animation PlayerBigWalk;
        private Animation PlayerBigJump;
        private Animation PlayerBigClimbPole;   //爬干
        private Animation PlayerBigCrouch;      //蹲伏
        private Animation PlayerBigHugPole;     //抱柱

        private Animation PlayerFireBigIdle;
        private Animation PlayerFireBigWalk;
        private Animation PlayerFireBigJump;
        private Animation PlayerFireBigClimbPole; //爬干
        private Animation PlayerFireBigCrouch; //蹲伏
        private Animation PlayerFireBigHugPole; //抱柱

        private Animation PlayerBigUltimateIdle;
        private Animation PlayerBigUltimateWalk;
        private Animation PlayerBigUltimateRun;
        private Animation PlayerBigUltimateJump;
        private Animation PlayerBigUltimateCrouch; //蹲伏

        private Animation PlayerTurnSmallIdle; // 变小 
        private Animation PlayerTurnSmallWalk; // 变小 走路

        private AnimationPlayer mAniPlayer;
        public PlayerMode Mode { get; internal set; }

        public float cameraPosX;
        bool IsOnGround;
        public Vector2 Velocity;
        public float movement;
        bool isJumping;
        private float previousBottom;
        private float previousTop;
        // ==================== Movement Constants ====================
        private const float MoveAcceleration = 13000.0f;
        private const float MaxMoveSpeed = 1750.0f;
        private const float GroundDragFactor = 0.48f;
        private const float AirDragFactor = 0.58f;

        // ==================== Jump Constants ====================
        // 马里奥式可变跳跃：轻按=小跳，长按=大跳；上升中松手即“截断”下降，最高高度有上限。
        // 高度以“格”为单位、按屏幕动态换算，保证不同分辨率下跳的格数一致。
        private const float MaxJumpTime = 0.15f;          // 全力跳（按住满）的上升持续时长（秒）
        private const float MaxJumpSpeed = -5600f;          // 起跳速度
        private const float MaxJumpHeightTiles = 4.0f;    // 全力跳的最高高度（格）：设上限，避免跳太高
        private const float JumpCutFactor = 0.4f;         // 上升中松手的“截断”系数：松手瞬间上升速度乘此值 → 小跳
        public const float GravityAcceleration = 3400.0f;
        private const float MaxFallSpeed = 550.0f;
        private float MaxJumpUpPosY = 0;
        private float MinJumpDownPosY = 0;

        // ==================== Input Configuration ====================
        private const float MoveStickScale = 1.0f;
        private const float AccelerometerScale = 1.5f;

        // ==================== Collision Detection ====================
        public override Rectangle Collider2DZone
        {
            get
            {
                return mAniPlayer.BoundingRectangle;
            }
        }

        private bool wasJumping;
        private bool jumpKeyHeld;          // 跳跃键当前是否被按住（用于可变跳跃：上升中是否仍在按住）
        private float initialFallYPosition;
        private bool isFalling;
        private float jumpTime;
        private const float MaxSafeFallDistance = -500f;
        public FaceDirection direction = FaceDirection.Right; //正向，反向

        public bool BigPlayer 
        { 
            get 
            {
                return nPlayerType == EPlayerType.BigPlayer ||
                     nPlayerType == EPlayerType.FireBigPlayer ||
                     nPlayerType == EPlayerType.UltimateBigPlayer;
            }
        }

        public EPlayerType nPlayerType = EPlayerType.Player;
        private EAniType nAniType = EAniType.Idle;
        private EPlayerState mPlayerState;
        public EPlayerModeScripting mPlayerModeScripting;

        public readonly LinkedList<Player_FireBall> mFireBallList = new LinkedList<Player_FireBall>();

        public Player(Level level, Vector2 worldPos)
        {
            this.mLevel = level;

            jumpSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_jump-small");
            jumpBigSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_jump-super");
            flagPoleSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_flagpole");
            pipeSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_pipe");
            dieSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_mariodie");
            oneUpSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_1-up");
            turnBigSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_powerup");
            coinSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_coin");
            kickSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_kick");
            endGameSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_world_clear");
            fireballSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_fireball");

            PlayerIdle = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 44, 44, 1.0f, true);
            PlayerWalk = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 45, 47, 15 / 60f, true);
            PlayerJump = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 49, 49, 1.0f, true);
            PlayerDie = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 57, 57, 30 / 60f, true);
            PlayerClimbPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 50, 51, 6 / 60f, true, true);
            PlayerHugPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 50, 50, 72 / 60f, true);

            PlayerUltimateIdle = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 191, 259, 325, 318, 191}, 1.0f / 20, true);
            PlayerUltimateWalk = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 195, 329, 322, 263, 191, 328, 321, 262, 196, 330, 323, 264, 196, 330, 323, 264 }, 1.0f / 20, true);
            PlayerUltimateRun = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 191, 322, 328, 262, 323, 196, 264, 330, 194, 262, 330, 196, 322, 195, 262, 328, 196, 323, 328, 262, 323, 196, 263, 322, 196}, 1.0f / 20, true);
            PlayerUltimateJump = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 197, 265, 324, 331, 197 }, 1.0f / 20, true);

            PlayerBigIdle = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 14, 14, 1.0f / 10, true);
            PlayerBigWalk = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 16, 18, 1.0f / 10, true, true);
            PlayerBigJump = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 20, 20, 1.0f / 10, true);
            PlayerBigClimbPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 21, 22, 1.0f / 10, true);
            PlayerBigCrouch = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 15, 15, 1.0f / 10, true);
            PlayerBigHugPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 21, 21, 1.0f / 10, true);

            PlayerFireBigIdle = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 248, 248, 1.0f / 10, false);
            PlayerFireBigWalk = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[]{251, 304, 252, 251}, 1.0f / 10, true);
            PlayerFireBigJump = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 307, 307, 1.0f / 10, false);
            PlayerFireBigClimbPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", new int[] { 137, 138, 137}, 1.0f / 10, true);
            PlayerFireBigCrouch = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 301, 301, 1.0f / 10, false);
            PlayerFireBigHugPole = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 137, 137, 1.0f / 10, false);

            PlayerBigUltimateIdle = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 220, 295, 358, 351, 220 }, 1.0f / 20, true);
            PlayerBigUltimateWalk = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 223, 361, 354, 298, 222, 360, 353, 297, 324, 362, 355, 299, 223 }, 1.0f / 20, true);
            PlayerBigUltimateRun = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 223, 298, 360, 222, 355, 362, 298, 354, 222, 297, 362, 224, 354, 223, 297, 360, 224, 355, 360, 297, 355, 224, 298, 354, 223 }, 1.0f / 20, true);
            PlayerBigUltimateJump = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 225, 300, 356, 363, 225 }, 1.0f / 20, true);
            PlayerBigUltimateCrouch = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", new int[] { 219, 294, 357, 350, 219 }, 1.0f / 20, true);

            PlayerTurnSmallIdle = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 44, 44, 1.0f / 10, false);
            PlayerTurnSmallWalk = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 44, 44, 1.0f / 10, false);

            mAniPlayer.Pivot = new Vector2(0, 1);
            mAniPlayer.mTransform = this;
            Reset(worldPos);
        }

        public void Reset(Vector2 position)
        {
            WorldPosition = BeginPos = position;
            Velocity = Vector2.Zero;
            mPlayerState =  EPlayerState.Normal;
            this.nPlayerType = EPlayerType.Player;
            PlayAnimation(EAniType.Idle);
            UpdateCameraPos(false);
        }

        public void SwitchPlayerType()
        {
            if (PlayerData.Instance.bEat_UltimateStar)
            {
                if(PlayerData.Instance.bEat_Mushroom || PlayerData.Instance.bEat_FireFlower)
                {
                    this.nPlayerType = EPlayerType.UltimateBigPlayer;
                }
                else
                {
                    this.nPlayerType = EPlayerType.UltimatePlayer;
                }
            }
            else if (PlayerData.Instance.bEat_FireFlower)
            {
                if (PlayerData.Instance.bEat_Mushroom)
                {
                    this.nPlayerType = EPlayerType.FireBigPlayer;
                }
                else
                {
                    this.nPlayerType = EPlayerType.BigPlayer;
                }
            }
            else if (PlayerData.Instance.bEat_Mushroom)
            {
                this.nPlayerType = EPlayerType.BigPlayer;
            }
            else
            {
                this.nPlayerType = EPlayerType.Player;
            }

            PlayPlayerBlinkAni();
            PlayAnimation(this.nAniType);
        }

        private void PlayPlayerBlinkAni()
        {
            var mTimer = KTimer.New(this, () =>
            {
                this.activeSelf = !this.activeSelf;
            }, 0.1f, 14);
            mTimer.Start();
        }


        public void PlayAnimation(EAniType nAniType)
        {
            this.nAniType = nAniType;
            mAniPlayer.PlayAnimation(GetAnimation());
        }

        public Animation GetAnimation()
        {
            if (this.nPlayerType == EPlayerType.Player)
            {
                switch (nAniType)
                {
                    case EAniType.Idle:
                        return PlayerIdle;
                    case EAniType.Walk:
                        return PlayerWalk;
                    case EAniType.Jump:
                        return PlayerJump;
                    case EAniType.ClimbPole:
                        return PlayerClimbPole;
                    case EAniType.HugPole:
                        return PlayerHugPole;
                    case EAniType.Die:
                        return PlayerDie;
                }
            }
            else if (this.nPlayerType == EPlayerType.BigPlayer)
            {
                switch (nAniType)
                {
                    case EAniType.Idle:
                        return PlayerBigIdle;
                    case EAniType.Walk:
                        return PlayerBigWalk;
                    case EAniType.Jump:
                        return PlayerBigJump;
                    case EAniType.ClimbPole:
                        return PlayerBigClimbPole;
                    case EAniType.HugPole:
                        return PlayerBigHugPole;
                    case EAniType.Crouch:
                        return PlayerBigCrouch;
                    default:
                        throw new NotSupportedException();
                }
            }
            else if (this.nPlayerType == EPlayerType.FireBigPlayer)
            {
                switch (nAniType)
                {
                    case EAniType.Idle:
                        return PlayerFireBigIdle;
                    case EAniType.Walk:
                        return PlayerFireBigWalk;
                    case EAniType.Jump:
                        return PlayerFireBigJump;
                    case EAniType.ClimbPole:
                        return PlayerFireBigClimbPole;
                    case EAniType.HugPole:
                        return PlayerFireBigHugPole;
                    case EAniType.Crouch:
                        return PlayerFireBigCrouch;
                    default:
                        throw new NotSupportedException();
                }
            }
            else if (this.nPlayerType == EPlayerType.UltimatePlayer)
            {
                switch (nAniType)
                {
                    case EAniType.Idle:
                        return PlayerUltimateIdle;
                    case EAniType.Walk:
                        return PlayerUltimateWalk;
                    case EAniType.Jump:
                        return PlayerUltimateJump;
                    case EAniType.ClimbPole:
                        return PlayerClimbPole;
                    case EAniType.HugPole:
                        return PlayerHugPole;
                    default:
                        throw new NotSupportedException();
                }
            }
            else if (this.nPlayerType == EPlayerType.UltimateBigPlayer)
            {
                switch (nAniType)
                {
                    case EAniType.Idle:
                        return PlayerBigUltimateIdle;
                    case EAniType.Walk:
                        return PlayerBigUltimateWalk;
                    case EAniType.Jump:
                        return PlayerBigUltimateJump;
                    case EAniType.ClimbPole:
                        return PlayerBigClimbPole;
                    case EAniType.HugPole:
                        return PlayerBigHugPole;
                    case EAniType.Crouch:
                        return PlayerBigUltimateCrouch;
                    default:
                        throw new NotSupportedException();
                }
            }

            throw new NotSupportedException();
        }

        public override void Draw()
        {
            if (PlayerData.Instance.bEat_FireFlower && PlayerData.Instance.bEat_Mushroom)
            {
                var mNode = mFireBallList.First;
                while (mNode != null)
                {
                    mNode.Value.Draw();
                    mNode = mNode.Next;
                }
            }

            if (activeSelf)
            {
                mAniPlayer.Draw(KSceneMgr.SpriteBatch, 
                    direction == FaceDirection.Right ? SpriteEffects.None : SpriteEffects.FlipHorizontally);

                DrawCollider2DZone();
            }
        }

        public override void Update()
        {
            mAniPlayer.Update();

            if (mPlayerState == EPlayerState.Normal || mPlayerState == EPlayerState.Blink)
            {
                if (Mode == PlayerMode.Playing)
                {
                    HandleInput();
                }
                else if(Mode == PlayerMode.Scripting)
                {
                    if(this.mPlayerModeScripting == EPlayerModeScripting.GameWin)
                    {
                        Velocity.Y = 0;
                        if (IsOnGround)
                        {
                            movement = 1;
                        }
                    }
                }

                Move();
            }

            UpdateCameraPos();

            if (Velocity.X < 0)
            {
                direction = FaceDirection.Left;
            }
            else if (Velocity.X > 0)
            {
                direction = FaceDirection.Right;
            }

            if(PlayerData.Instance.bEat_UltimateStar)
            {
                PlayerData.Instance.UltimateStarTime -= KTime.deltaTime;
                if(PlayerData.Instance.UltimateStarTime <= 0)
                {
                    PlayerData.Instance.bEat_UltimateStar = false;
                    PlayerData.Instance.UltimateStarTime = 0f;
                    SwitchPlayerType();
                }
            }

            if(PlayerData.Instance.bEat_FireFlower && PlayerData.Instance.bEat_Mushroom)
            {
                var mNode = mFireBallList.First;
                while (mNode != null)
                {
                    mNode.Value.Update();
                    if (mNode.Value.IsDispose)
                    {
                        var curNode = mNode;
                        mNode = mNode.Next;
                        mFireBallList.Remove(curNode);
                    }
                    else
                    {
                        mNode = mNode.Next;
                    }
                }
            }
            else if(mFireBallList.Count > 0)
            {
                mFireBallList.Clear();
            }

        }

        private void HandleInput()
        {
            if (KInputMgr.GetKey(Keys.A) || KInputMgr.GetKey(Keys.Left))
            {
                movement = -1.0f;
            }
            else if (KInputMgr.GetKey(Keys.D) || KInputMgr.GetKey(Keys.Right))
            {
                movement = 1.0f;
            }

            // 跳跃：用 GetKeyDown（边沿）在“按下那一帧”发起一次起跳；
            // 同时用 GetKey（按住）记录 jumpKeyHeld，供 DoJump 判断上升中是否仍在按住
            // （按住=大跳，松手=截断成小跳），从而实现马里奥式可变跳跃高度。
            jumpKeyHeld = KInputMgr.GetKey(Keys.Up) || KInputMgr.GetKey(Keys.W);
            if (KInputMgr.GetKeyDown(Keys.Up) || KInputMgr.GetKeyDown(Keys.W))
            {
                isJumping = true;
                PlayAnimation(EAniType.Jump);
            }

            if (PlayerData.Instance.bEat_FireFlower && PlayerData.Instance.bEat_Mushroom)
            {
                if (KInputMgr.GetKeyDown(Keys.Space))
                {
                    SendFireBall();
                }
            }
        }

        public void Move()
        {

            ApplyPhysics();
            if (IsOnGround)
            {
                if (BigPlayer && (KInputMgr.GetKey(Keys.Down) || KInputMgr.GetKey(Keys.S)))
                {
                    PlayAnimation(EAniType.Crouch);
                }
                else
                {
                    if (Math.Abs(Velocity.X) - 0.02f > 0)
                    {
                        PlayAnimation(EAniType.Walk);
                    }
                    else
                    {
                        PlayAnimation(EAniType.Idle);
                    }
                }
            }

            movement = 0.0f;
            isJumping = false;
        }

        public void ApplyPhysics()
        {
            float elapsed = KTime.deltaTime;
            Velocity.X += movement * MoveAcceleration * elapsed;
            Velocity.Y = MathHelper.Clamp(Velocity.Y + GravityAcceleration * elapsed, -MaxFallSpeed, MaxFallSpeed);
            Velocity.Y = DoJump(Velocity.Y);

            if (IsOnGround)
            {
                Velocity.X *= GroundDragFactor;
            }
            else
            {
                Velocity.X *= AirDragFactor;
            }

            Velocity.X = MathHelper.Clamp(Velocity.X, -MaxMoveSpeed, MaxMoveSpeed);
            float spendTime = elapsed;
            float fixedTime1 = elapsed;
            float fixedTime2 = elapsed;

            float Coef = 0.5f;
            if (Math.Abs(Velocity.Y * elapsed) >= Tile.TileHeight * Coef)
            {
                float step = Math.Abs(Velocity.Y * elapsed) / (Tile.TileHeight * Coef);
                fixedTime1 = elapsed / step;
            }

            if (Math.Abs(Velocity.X * elapsed) >= Tile.TileWidth * Coef)
            {
                float step = Math.Abs(Velocity.X * elapsed) / (Tile.TileWidth * Coef);
                fixedTime2 = elapsed / step;
            }

            float fixedTime = Math.Min(fixedTime1, fixedTime2);
            while (spendTime > 0)
            {
                spendTime -= fixedTime;

                Vector2 previousPosition = WorldPosition;
                WorldPosition += Velocity * fixedTime;
                WorldPosition = new Vector2((float)Math.Round(WorldPosition.X), (float)Math.Round(WorldPosition.Y));

                HandleCollisions();
                
                if (WorldPosition.X == previousPosition.X)
                {
                    Velocity.X = 0;
                }

                if (WorldPosition.Y == previousPosition.Y)
                {
                    Velocity.Y = 0;
                }
            }

        }

        private float DoJump(float velocityY)
        {
            // 起跳：仅“按下那一帧 + 在地面”触发一次。
            if (isJumping && !wasJumping && IsOnGround)
            {
                jumpTime = 0.0001f;
                if (BigPlayer)
                {
                    jumpBigSound.Play();
                }
                else
                {
                    jumpSound.Play();
                }
            }

            if (jumpTime > 0.0f)
            {
                // 上升阶段（可变跳跃核心）：
                // 1) 只要仍在按住跳跃键，就维持恒定向上速度（火箭推力），按得越久上升越久 → 越高。
                // 2) 若在上升中松开按键，立即把上升速度乘 JumpCutFactor 截断，并结束推力交给重力 → 小跳。
                // 3) 若按住到 MaxJumpTime，结束推力，达到本次跳跃的最高高度上限（MaxJumpHeightTiles 格）。
                // 高度以格为单位按当前 Tile.TileHeight 换算，适配不同屏幕分辨率。
                float launch = MaxJumpHeightTiles * Tile.TileHeight / MaxJumpTime * 2;
                launch = launch * (1.0f - Math.Clamp(jumpTime / MaxJumpTime, 0, 1));
                launch = Math.Max(MaxJumpSpeed * (1.0f - Math.Clamp(jumpTime / MaxJumpTime, 0, 1)), launch);
                launch = Math.Max(velocityY, launch);
                velocityY = -launch;

                if (!jumpKeyHeld)
                {
                    // 上升中松手：截断——削减当前上升速度，之后由重力自然拉回（小跳/中跳）
                    velocityY *= JumpCutFactor;
                    jumpTime = 0.0f;
                }
                else
                {
                    jumpTime += KTime.deltaTime;
                    if (jumpTime >= MaxJumpTime)
                    {
                        jumpTime = 0.0f; // 全力跳满，达到高度上限，交回重力
                    }
                }

                isFalling = false;
            }
            else
            {
                // 非上升阶段：维护掉落伤害判定
                jumpTime = 0.0f;
                if (!IsOnGround && !isJumping && !isFalling)
                {
                    initialFallYPosition = WorldPosition.Y;
                    isFalling = true;
                }

                if (IsOnGround && isFalling)
                {
                    float fallDistance = initialFallYPosition - WorldPosition.Y;
                    if (fallDistance < MaxSafeFallDistance)
                    {
                        OnKilled(null);
                    }
                    isFalling = false;
                }
            }

            wasJumping = isJumping;
            return velocityY;
        }

        private void HandleCollisions()
        {
            Rectangle bounds = Collider2DZone;
            int leftTile = (int)Math.Floor((float)bounds.Left / Tile.TileWidth) - 2;
            int rightTile = (int)Math.Ceiling(((float)bounds.Right / Tile.TileWidth)) + 2;
            int topTile = (int)Math.Floor((float)bounds.Top / Tile.TileHeight) - 2;
            int bottomTile = (int)Math.Ceiling(((float)bounds.Bottom / Tile.TileHeight)) + 2;

            IsOnGround = false;
            for (int y = topTile; y <= bottomTile; ++y)
            {
                for (int x = leftTile; x <= rightTile; ++x)
                {
                    Tile mTile = mLevel.GetTile(x, y);
                    TileCollision collision = mLevel.GetCollision(x, y);
                    if (collision != TileCollision.Passable)
                    {
                        TileBase mTarget = mTile.Target;
                        Rectangle tileBounds = mTarget != null ? mTarget.Collider2DZone : mLevel.GetTileRectangle(x, y);
                        Vector2 depth = RectangleExtensions.GetIntersectionDepth(bounds, tileBounds);
                        //如果重叠区域 大于0
                        if (depth != Vector2.Zero)
                        {
                            float absDepthX = Math.Abs(depth.X);
                            float absDepthY = Math.Abs(depth.Y);

                            if (collision == TileCollision.Impassable)
                            {
                                //优先重叠区域小的 更新位置
                                //这里是物理：重叠越小，顶撞的力量最容易在这里泄出去
                                //这里是物理：分开的距离/代价越小，顶撞的力量最容易在这里泄出去
                                //有可能 玩家正在正下方，但是 absDepthX < absDepthY, 所以我们得 加上absDepthX <= tileBounds.Size.X / 2f
                                if (absDepthX < absDepthY && absDepthX <= tileBounds.Size.X / 2f)
                                {
                                    WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                }
                                else
                                {
                                    if (previousBottom <= tileBounds.Top)
                                    {
                                        IsOnGround = true;
                                    }

                                    //现在我检测每帧移动，如果速度过大，我会增大检测频率，所以这里用不到了
                                    //if (Velocity.Y < 0)
                                    //{
                                    //    //向上顶的时候，有个反弹速度
                                    //    Velocity.Y = -Velocity.Y / 2f;
                                    //}
                                    WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                    if (previousTop >= tileBounds.Bottom)
                                    {
                                        jumpTime = 0.0f; //碰撞完后，就把跳跃时间归零
                                        Velocity.Y = 0; // 把向上的速度也归0
                                        if (mTile.Target is QuestionBlock)
                                        {
                                            QuestionBlock mQuestionBlock = mTile.Target as QuestionBlock;
                                            mQuestionBlock.HandleHit(this);
                                        }
                                        else if (mTile.Target is CoinBlock)
                                        {
                                            CoinBlock mCoinBlock = mTile.Target as CoinBlock;
                                            mCoinBlock.HandleHit(this);
                                        }
                                    }
                                }
                                bounds = Collider2DZone;
                            }
                            else if (collision == TileCollision.Breakable)
                            {
                                if (BigPlayer)
                                {
                                    if (absDepthX < absDepthY && absDepthX <= tileBounds.Size.X / 3f)
                                    {
                                        WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                    }
                                    else
                                    {
                                        if (previousBottom <= tileBounds.Top)
                                        {
                                            IsOnGround = true;
                                        }

                                        if (previousTop >= tileBounds.Bottom)
                                        {
                                            jumpTime = 0.0f; //碰撞完后，就把跳跃时间归零
                                            //向上顶的时候，有个反弹速度
                                            Velocity.Y /= 2f;
                                            if (mTile.Target is BreakableBlock)
                                            {
                                                BreakableBlock mBreakableBlock = mTile.Target as BreakableBlock;
                                                mBreakableBlock.HandleHit(true);
                                                mLevel.BreakTile(x, y);
                                            }
                                        }
                                        else
                                        {
                                            WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                        }
                                    }
                                }
                                else
                                {
                                    //这里是物理：重叠越小，顶撞的力量最容易在这里泄出去
                                    if (absDepthX < absDepthY)
                                    {
                                        WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                    }
                                    else
                                    {
                                        if (previousBottom <= tileBounds.Top)
                                        {
                                            IsOnGround = true;
                                        }

                                        //if (Velocity.Y < 0)
                                        //{
                                        //    //向上顶的时候，有个反弹速度
                                        //    Velocity.Y = -Velocity.Y / 2f;
                                        //}
                                        WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                        if (previousTop >= tileBounds.Bottom)
                                        {
                                            if (mTile.Target is BreakableBlock)
                                            {
                                                BreakableBlock mBreakableBlock = mTile.Target as BreakableBlock;
                                                mBreakableBlock.HandleHit(false);
                                            }
                                        }
                                    }
                                }

                                bounds = Collider2DZone;
                            }
                            else if (collision == TileCollision.Platform)
                            {
                                if (depth.Y < 0 && previousBottom > tileBounds.Top)
                                {
                                    WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                    IsOnGround = true;
                                }
                                else
                                {
                                    if (absDepthX < absDepthY)
                                    {
                                        WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                    }
                                    else
                                    {
                                        WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                    }

                                }

                                bounds = Collider2DZone;
                            }
                            else if (collision == TileCollision.Exit)
                            {
                                if (this.mPlayerModeScripting != EPlayerModeScripting.GameWin)
                                {
                                    Velocity.X = 0;
                                    Velocity.Y = 0;
                                    PlayAnimation(EAniType.HugPole);
                                    WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                    CastleBlock mCastleBlock = mTile.Target as CastleBlock;
                                    mCastleBlock.DoPlayerAni(this);
                                    this.Mode = PlayerMode.Scripting;
                                    this.mPlayerModeScripting = EPlayerModeScripting.GameWin;
                                    IsOnGround = false;
                                    flagPoleSound.Play();
                                    KTween.delayedCall(1.5f, () =>
                                    {
                                        endGameSound.Play();
                                    });
                                }
                            }
                        }
                    }
                }
            }

            foreach (var mTarget in mLevel.mPowerUpObjectList)
            {
                if (!mTarget.IsDispose && mTarget.orCanEat())
                {
                    Rectangle tileBounds = mTarget.Collider2DZone;
                    Vector2 depth = RectangleExtensions.GetIntersectionDepth(bounds, tileBounds);
                    if (depth != Vector2.Zero)
                    {
                        if (mTarget is PowerUp_Mushroom)
                        {
                            PowerUp_Mushroom mMushroom = mTarget as PowerUp_Mushroom;
                            mMushroom.OnKilled(this);
                            PlayAnimation(nAniType);
                            turnBigSound.Play();

                            PlayerData.Instance.Eat_PowerUp_Mushroom();
                            SwitchPlayerType();
                        }
                        else if (mTarget is PowerUp_OneUpMushroom)
                        {
                            PowerUp_OneUpMushroom mMushroom = mTarget as PowerUp_OneUpMushroom;
                            mMushroom.OnKilled(this);
                            oneUpSound.Play();
                            PlayerData.Instance.Eat_PowerUp_OneUpMushroom();
                            SwitchPlayerType();
                        }
                        else if (mTarget is PowerUp_FireFlower)
                        {
                            PowerUp_FireFlower mMushroom = mTarget as PowerUp_FireFlower;
                            mMushroom.OnKilled(this);
                            turnBigSound.Play();
                            PlayerData.Instance.Eat_PowerUp_FireFlower();
                            SwitchPlayerType();
                        }
                        else if (mTarget is PowerUp_UltimateStar)
                        {
                            PowerUp_UltimateStar mMushroom = mTarget as PowerUp_UltimateStar;
                            mMushroom.OnKilled(this);
                            turnBigSound.Play();
                            PlayerData.Instance.Eat_PowerUp_UltimateStar();
                            SwitchPlayerType();
                        }
                    }
                }
            }

            foreach (var mEnemy in mLevel.mEnemyObjectList)
            {
                if (!mEnemy.IsDispose)
                {
                    Rectangle tileBounds = mEnemy.Collider2DZone;
                    Vector2 depth = RectangleExtensions.GetIntersectionDepth(bounds, tileBounds);
                    if (depth != Vector2.Zero)
                    {
                        if (mEnemy.GetState() == EObjectState.Ok)
                        {
                            if (PlayerData.Instance.bEat_UltimateStar)
                            {
                                mEnemy.OnKilled(this);
                            }
                            else
                            {
                                //在 板栗崽的 头顶 才能杀死板栗崽
                                if (previousBottom <= tileBounds.Top)
                                {
                                    //玩家杀死敌人的同时，会有一个反向的速度
                                    mEnemy.OnKilled(this);
                                    this.Velocity = -this.Velocity * 0.5f;
                                }
                                else
                                {
                                    this.OnKilled(mEnemy);
                                }
                            }
                        }
                        else if (mEnemy.GetState() == EObjectState.Deading)
                        {
                            mEnemy.OnKilled(this);
                        }
                    }
                }
            }

            if (bounds.Top >= Tile.TileFloorY)
            {
                OnKilled(null);
            }
            previousBottom = bounds.Bottom;
            previousTop = bounds.Top;
        }

        public void OnKilled(EnemyBase killedBy)
        {
            if (killedBy == null)
            {
                if (mPlayerState == EPlayerState.Normal)
                {
                    dieSound.Play();
                    mPlayerState = EPlayerState.Deading;
                    KTween.delayedCall(5.0f, () =>
                    {
                        mPlayerState = EPlayerState.Dead;
                        (KSceneMgr.Main as MainScene).ReloadCurrentLevel();
                    });
                }
            }
            else
            {
                if (PlayerData.Instance.bEat_UltimateStar) return;

                if (BigPlayer)
                {
                    if (mPlayerState == EPlayerState.Normal)
                    {
                        PlayerData.Instance.bEat_FireFlower = false;
                        PlayerData.Instance.bEat_Mushroom = false;
                        PlayerData.Instance.bEat_UltimateStar = false;

                        mPlayerState = EPlayerState.Blink;
                        //变小
                        KTweenEx.scale(this, Vector2.One * 0.5f, 0.2f).SetOnCompleteFunc(() =>
                        {
                            this.LocalScale = Vector2.One;
                            SwitchPlayerType();
                            PlayAnimation(EAniType.Idle);
                            pipeSound.Play();

                            KTween.delayedCall(this, 1f, () =>
                            {
                                mPlayerState = EPlayerState.Normal;
                            });
                        });
                    }
                }
                else
                {
                    if (mPlayerState == EPlayerState.Normal)
                    {
                        if (--PlayerData.Instance.nLefeCount == 0)
                        {
                            mPlayerState = EPlayerState.Deading;
                            dieSound.Play();
                            PlayAnimation(EAniType.Die);

                            KTween.delayedCall(5.0f, () =>
                            {
                                mPlayerState = EPlayerState.Dead;
                                (KSceneMgr.Main as MainScene).ReloadCurrentLevel();
                            });
                        }
                        else
                        {
                            mPlayerState = EPlayerState.Blink;
                            //播放闪烁动画
                            PlayPlayerBlinkAni();

                            KTween.delayedCall(this, 1f, () =>
                            {
                                mPlayerState = EPlayerState.Normal;
                            });
                        }
                    }
                }
            }
        }

        public Matrix4x4 World_To_View_Matrix
        {
            get
            {
                return Matrix4x4.CreateTranslation(-cameraPosX, 0, 0);
            }
        }

        private float fLastCameraOffsetX = 0;
        private float fCameraOffsetX = 0;
        private void UpdateCameraPos(bool bPlayAni = true)
        {
            int nType = 3;
            if (nType == 1)
            {
                cameraPosX = WorldPosition.X - KSceneMgr.Game.GraphicsDevice.Viewport.Width / 2f;
            }
            else if (nType == 2)
            {
                float nOffsetX = 0;
                if (direction == FaceDirection.Left)
                {
                    nOffsetX = KSceneMgr.Game.GraphicsDevice.Viewport.Width / 3f * 2;
                }
                else
                {
                    nOffsetX = KSceneMgr.Game.GraphicsDevice.Viewport.Width / 3f;
                }

                if (fLastCameraOffsetX != nOffsetX)
                {
                    if (bPlayAni)
                    {
                        float From = fLastCameraOffsetX;
                        float To = nOffsetX;
                        KTween.AddTween(0.5f, fPercent =>
                        {
                            fCameraOffsetX = MathHelper.LerpPrecise(From, To, fPercent);
                        });
                    }
                    else
                    {
                        fCameraOffsetX = nOffsetX;
                    }
                    fLastCameraOffsetX = nOffsetX;
                }

                cameraPosX = WorldPosition.X - fCameraOffsetX;
            }
            else if (nType == 3)
            {
                float fCameraMinX = Tile.TileMinPosX;
                float fCameraMaxX = Tile.TileMaxPosX;
                cameraPosX = WorldPosition.X - KSceneMgr.Game.GraphicsDevice.Viewport.Width / 2f;
                if(cameraPosX < fCameraMinX)
                {
                    cameraPosX = fCameraMinX;
                }

                if (cameraPosX > fCameraMaxX)
                {
                    cameraPosX = fCameraMaxX;
                }

            }

        }

        private void SendFireBall()
        {
            new Player_FireBall(mLevel, this, WorldPosition);
            fireballSound.Play();
        }

    }
}