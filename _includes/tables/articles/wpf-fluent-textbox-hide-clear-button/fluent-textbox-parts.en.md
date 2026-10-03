| window | Style applied | named parts present | Padding.Left |
|---|---|---|---|
| no ThemeMode | theme style | PART\_ContentHost | 0 |
| ThemeMode=Light | implicit style | DeleteButton, PART\_ContentHost | 10 |
| ThemeMode=Light + implicit Style | implicit style | PART\_ContentHost | 8 |
| no ThemeMode + implicit Style | implicit style | PART\_ContentHost | 8 |
| merge Fluent.xaml directly | implicit style | DeleteButton, PART\_ContentHost | 10 |
| merge Fluent.xaml + implicit Style | implicit style | PART\_ContentHost | 8 |
| ThemeMode=Light + implicit Style, BasedOn | implicit style | DeleteButton, PART\_ContentHost | 8 |
| merge Fluent.xaml + implicit Style, BasedOn | implicit style | DeleteButton, PART\_ContentHost | 8 |
