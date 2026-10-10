using Novolis.Math.Arrays;

var grid = new DenseGrid<char>(8, 8);
grid.Set(new GridIndex(0, 0), 'N');
grid.Set(new GridIndex(1, 0), 'o');
grid.Set(new GridIndex(2, 0), 'v');
grid.Set(new GridIndex(3, 0), 'o');
grid.Set(new GridIndex(4, 0), 'l');
grid.Set(new GridIndex(5, 0), 'i');
grid.Set(new GridIndex(6, 0), 's');

Console.WriteLine($"Dogfood {grid.Width}x{grid.Height}: {grid.Get(new GridIndex(0, 0))}{grid.Get(new GridIndex(6, 0))}…");
