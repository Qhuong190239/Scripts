using System.Collections.Generic;
using System;
using UnityEngine;
using Random=System.Random;
public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion 
}
public class Chessboard : MonoBehaviour
{
    [Header("Art Stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deadthSize = 0.3f;
    [SerializeField] private float deadthSpacing = 0.3f;
    [SerializeField] private float dragOffset= 1.5f;
    [SerializeField] private GameObject victoryScreen;
    
    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    // LOGIC
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private GameObject[,] tiles; 
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    private void Awake()
    {
        isWhiteTurn = true;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        
        SpawnAllPieces();

        PositionAllPieces();
    }
    private void Update()
    {
        if(!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            // Get the indexes of tile have hit
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // If hovering a tile after not hovering any tiles
            if(currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // If already hovering a tile, change the precious
            if(currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // If press down the mouse
            if(Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    // Check whick turn
                    if((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        // Get a list and highlight tile that can go
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        // Get a list of special moves
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                                                
                        HighlightTiles();
                    }
                }
            }

            // If releasing the mouse button
            if(currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                bool validMove = MoveTo(currentlyDragging, hitPosition.x, hitPosition.y);
                if(!validMove)
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                
                currentlyDragging = null;
                RemoveHighlightTiles();

            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if(currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }
        
        // When dragging a piece
        if(currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if(horizontalPlane.Raycast(ray, out distance))
            {
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            }
        }
    }

    // Generate board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX/2) * tileSize, 0, (tileCountY/2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for(int x = 0; x < tileCountX; x++)
            for(int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize )- bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x+1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }
    // Random method

    // private static Random rnd = new Random();
 
    // public static T PickRandom<T>(this IList<T> source)
    // {
    //     int randIndex = rnd.Next(source.Count);
    //     return source[randIndex];
    // }

    // public static void Random()
    // {
    //     List<int> numbers = new List<int>() { 1, 2, 3, 4, 5 };
 
    //     int random = numbers.PickRandom();
    // }

    static Random rnd = new Random();
 
    public static IEnumerable<T> PickRandom<T>(this IList<T> source, int count)
    {
        return source.OrderBy(x => rnd.Next()).Take(count);
    }

    // Spawning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        int whiteTeam = 0;
        int blackTeam = 1;

        chessPieces[randomNums, randomNums] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);        
        // chessPieces[ra,ra] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        // chessPieces[ra,ra] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        // chessPieces[ra,ra] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        // chessPieces[ra,ra] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);


        // White Team
        // chessPieces[0,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        // chessPieces[1,0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        // chessPieces[2,0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        // chessPieces[4,0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        // chessPieces[3,0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        // chessPieces[5,0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        // chessPieces[6,0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        // chessPieces[7,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        // for(int i = 0; i < TILE_COUNT_X; i++)
        //    chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
    
        // Black Team
        // chessPieces[0,7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        // chessPieces[1,7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        // chessPieces[2,7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        // chessPieces[4,7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        // chessPieces[3,7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        // chessPieces[5,7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        // chessPieces[6,7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        // chessPieces[7,7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        // for(int i = 0; i < TILE_COUNT_X; i++)
        //    chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);    
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();
        
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[((team == 0) ? 0 : 6) + ((int)type - 1)];

        return cp;
    }

    // Positioning
    private void PositionAllPieces()
    {
        for(int x = 0; x < TILE_COUNT_X; x++)
            for(int y = 0; y < TILE_COUNT_Y; y++)
                if(chessPieces[x,y] != null)
                    PositionSinglePiece(x, y, true);

    }
    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x,y].currentX = x;
        chessPieces[x,y].currentY = y;
        chessPieces[x,y].SetPosition(GetTileCenter(x, y), force);
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2); 
    }
    // Highlight Tiles
    private void HighlightTiles()
    {
        for(int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHighlightTiles()
    {
        for(int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }
    
    // Checkmate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnResetButton()
    {
        //UI
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false); 

        victoryScreen.SetActive(false);

        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        // Clean up
        for(int x = 0; x < TILE_COUNT_X; x++)
        {
            for(int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null)
                {
                    Destroy(chessPieces[x,y].gameObject);
                }
                chessPieces[x,y] = null;
            }
        }
        for(int i = 0; i < deadWhites.Count; i++)
        {
            Destroy(deadWhites[i].gameObject);
        }
        for(int i = 0; i < deadBlacks.Count; i++)
        {
            Destroy(deadBlacks[i].gameObject);
        }
        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
    }
    public void OnExitButton()
    {
        Application.Quit();
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];

            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if(myPawn.currentX == enemyPawn.currentX)
            {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if(enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deadthSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2) + (Vector3.forward * deadthSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deadthSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2) + (Vector3.forward * deadthSpacing) * deadBlacks.Count);
    
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if(specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn)
            {
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x,lastMove[1].y].transform.position; 
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                if(targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x,lastMove[1].y].transform.position; 
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        if(specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            // Left Rook
            if(lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0)     // White side
                {
                    ChessPiece rook = chessPieces[0,0];
                    chessPieces[3,0] = rook;
                    PositionSinglePiece(3,0);
                    chessPieces[0,0] = null;
                }
                else if (lastMove[1].y == 7) // Black side
                {
                    ChessPiece rook = chessPieces[0,7];
                    chessPieces[3,7] = rook;
                    PositionSinglePiece(3,7);
                    chessPieces[0,7] = null;
                }
            }
            // Right Rook
            else if(lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)     // White side
                {
                    ChessPiece rook = chessPieces[7,0];
                    chessPieces[5,0] = rook;
                    PositionSinglePiece(5,0);
                    chessPieces[7,0] = null;
                }
                else if (lastMove[1].y == 7) // Black side
                {
                    ChessPiece rook = chessPieces[7,7];
                    chessPieces[5,7] = rook;
                    PositionSinglePiece(5,7);
                    chessPieces[7,7] = null;
                }                
            }
        }
    }

    // Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for(int i = 0; i<moves.Count;i++)
            if(moves[i].x == pos.x && moves[i].y == pos.y)
                return true;

        return false;
    }
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if(!ContainsValidMove(ref availableMoves, new Vector2Int(x,y)))
        {
            return false;
        }

        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);
        
        // check target position
        if(chessPieces[x,y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if(cp.team == ocp.team)
            {
                return false;
            }

            // If it's the enemy team
            if(ocp.team == 0)
            {
                if(ocp.type == ChessPieceType.King)
                {
                    CheckMate(1);
                }

                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deadthSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2) + (Vector3.forward * deadthSpacing) * deadWhites.Count);
            }
            else
            {
                if(ocp.type == ChessPieceType.King)
                {
                    CheckMate(0);
                }                

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deadthSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2) + (Vector3.back * deadthSpacing) * deadBlacks.Count);
            }
        }

        chessPieces[x,y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x,y);

        isWhiteTurn = !isWhiteTurn;
        moveList.Add(new Vector2Int[] {previousPosition, new Vector2Int(x,y)});

        ProcessSpecialMove();

        return true;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for(int x = 0; x < TILE_COUNT_X; x++)
            for(int y = 0; y < TILE_COUNT_Y; y++)
                if(tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one;
    }
}
