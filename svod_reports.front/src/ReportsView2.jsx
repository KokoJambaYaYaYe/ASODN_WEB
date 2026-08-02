import React, { useState, useEffect, useRef } from 'react';
import { Box, Typography, CircularProgress, Button } from '@mui/material';
import PrintIcon from '@mui/icons-material/Print'; // Иконка принтера
import DownloadIcon from '@mui/icons-material/Download';
import { Workbook } from '@fortune-sheet/react';
import '@fortune-sheet/react/dist/index.css';

export default function ReportsView({ reportId }) {
    const VITE_BACKEND_API_URL = import.meta.env.VITE_BACKEND_API_URL;

    // Ссылка на оригинальный объект книги SheetJS, чтобы напечатать её в любой момент
    const rawWorkbookRef = useRef(null);
    // Ссылка на скрытый iframe для изоляции печатной формы
    const iframeRef = useRef(null);

    const [sheetData, setSheetData] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    useEffect(() => {
        if (!reportId) {
            setSheetData(null);
            return;
        }

        setLoading(true);
        setError(null);
        setSheetData(null);
        rawWorkbookRef.current = null;

        // Запрашиваем бинарный файл Excel, генерируемый вашим контроллером
        fetch(`${VITE_BACKEND_API_URL}/get-excel-report`)
            .then((response) => {
                if (!response.ok) {
                    throw new Error(`Ошибка сервера: ${response.status}`);
                }
                return response.json(); // Обычный легкий JSON парсинг силами браузера
            })
            .then((data) => {
                setSheetData(data); // Сразу передаем массив листов в FortuneSheet
                setLoading(false);
            })
            .catch((err) => {
                console.error('Excel Preview Error:', err);
                setError(err.message || 'Не удалось загрузить предпросмотр отчета.');
                setLoading(false);
            });
    }, [reportId]);

    const handlePrint = () => {
        // Проверяем, что данные загружены и iframe доступен
        if (!sheetData || sheetData.length === 0 || !iframeRef.current) return;

        // 1. Берем первый (активный) лист из структуры данных
        const activeSheet = sheetData[0];
        const celldata = activeSheet.celldata || [];

        // Находим максимальные индексы строк и колонок, чтобы построить правильную сетку HTML-таблицы
        let maxRow = 0;
        let maxCol = 0;
        celldata.forEach(item => {
            if (item.r > maxRow) maxRow = item.r;
            if (item.c > maxCol) maxCol = item.c;
        });

        // 2. Создаем двумерный массив (матрицу) нужного размера, заполненный пустыми строками
        const matrix = Array.from({ length: maxRow + 1 }, () =>
            Array(maxCol + 1).fill('')
        );

        // Заполняем матрицу отображаемым текстом (свойства "m" из бэкенда)
        celldata.forEach(item => {
            if (item.v && item.v.m) {
                matrix[item.r][item.c] = item.v.m;
            }
        });

        // 3. Генерируем чистую HTML-строку таблицы из матрицы
        let htmlTable = '<table>';
        matrix.forEach(row => {
            htmlTable += '<tr>';
            row.forEach(cellText => {
                htmlTable += `<td>${cellText}</td>`;
            });
            htmlTable += '</tr>';
        });
        htmlTable += '</table>';

        // 4. Формируем полноценный HTML-документ со стилями для отправки на принтер
        const printDocument = `
    <!DOCTYPE html>
    <html>
      <head>
        <title>Печать отчета</title>
        <style>
          body { font-family: Arial, sans-serif; padding: 20px; color: #1e293b; }
          table { border-collapse: collapse; width: 100%; margin-top: 10px; }
          td { border: 1px solid #cbd5e1; padding: 8px; text-align: left; font-size: 14px; min-height: 20px; }
          /* Делаем первую строку (шапку) визуально выделющейся */
          tr:first-child td { background-color: #f8fafc; font-weight: bold; }
        </style>
      </head>
      <body>
        <h3>${activeSheet.name || 'Отчет'}</h3>
        ${htmlTable}
      </body>
    </html>
  `;

        // 5. Записываем сгенерированный HTML внутрь скрытого iframe
        const iframeDoc = iframeRef.current.contentWindow.document;
        iframeDoc.open();
        iframeDoc.write(printDocument);
        iframeDoc.close();

        // 6. Вызываем системное окно печати браузера для этого фрейма
        setTimeout(() => {
            iframeRef.current.contentWindow.focus();
            iframeRef.current.contentWindow.print();
        }, 200);
    };

    // 2. Функция для скачивания файла напрямую с бэкенда
    const handleDownload = () => {
        // Создаем временную ссылку для скачивания файла
        const link = document.createElement('a');
        link.href = `${VITE_BACKEND_API_URL}/download-excel-report`;
        link.setAttribute('download', `report_${reportId}.xlsx`); // Имя файла при скачивании
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };


    if (!reportId) {
        return (
            <Box sx={{ p: 4, textAlign: 'center', border: '1px dashed #cbd5e1', borderRadius: '12px', mt: 2 }}>
                <Typography variant="body1" color="text.secondary">
                    Выберите отчет для просмотра.
                </Typography>
            </Box>
        );
    }

    if (loading) {
        return (
            <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: 'calc(100vh - 64px)', gap: 2 }}>
                <CircularProgress />
                <Typography variant="body2" color="text.secondary">
                    Формирование Excel отчета...
                </Typography>
            </Box>
        );
    }

    if (error) {
        return (
            <Box sx={{ p: 4, textAlign: 'center', border: '1px solid #fee2e2', backgroundColor: '#fef2f2', borderRadius: '12px', mt: 2 }}>
                <Typography variant="body1" color="error" sx={{ fontWeight: 'medium' }}>
                    Произошла ошибка при загрузке отчета
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    {error}
                </Typography>
            </Box>
        );
    }

    return (
        <Box sx={{ width: '100%', height: 'calc(100vh - 64px)', position: 'relative' }}>

            {/* ЕДИНЫЙ КОНТЕЙНЕР ДЛЯ КНОПОК УПРАВЛЕНИЯ */}
            {sheetData && (
                <Box sx={{
                    position: 'absolute',
                    top: '8px',
                    right: '60px',  // Оставляет место для крестика закрытия Dialog
                    zIndex: 1300,   // Выше холста FortuneSheet
                    display: 'flex',
                    gap: '10px'     // Аккуратный отступ между кнопками
                }}>
                    {/* КНОПКА СКАЧАТЬ */}
                    <Button
                        variant="contained"
                        onClick={handleDownload}
                        startIcon={<DownloadIcon />}
                        sx={{
                            textTransform: 'none',
                            borderRadius: '8px',
                            backgroundColor: '#ffffff', // Белая кнопка для контраста на зеленом
                            color: '#16a34a',           // Зеленый текст/иконка
                            fontWeight: 600,
                            '&:hover': { backgroundColor: '#f0fdf4' }, // Легкий зеленый бэкграунд при наведении
                            boxShadow: '0 2px 8px rgba(0,0,0,0.15)'
                        }}
                    >
                        Скачать
                    </Button>

                    {/* КНОПКА ПЕЧАТИ */}
                    <Button
                        variant="contained"
                        onClick={handlePrint}
                        startIcon={<PrintIcon />}
                        sx={{
                            textTransform: 'none',
                            borderRadius: '8px',
                            backgroundColor: '#16a34a', // Оставляем ваш зеленый цвет
                            color: '#ffffff',
                            '&:hover': { backgroundColor: '#15803d' },
                            boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
                            border: '1px solid rgba(255,255,255,0.4)' // Тонкая рамка, чтобы выделялась на зеленом
                        }}
                    >
                        Печать
                    </Button>
                </Box>
            )}

            {/* СКРЫТЫЙ IFRAME */}
            <iframe
                ref={iframeRef}
                title="Excel-Print-Internal"
                style={{ position: 'absolute', width: 0, height: 0, border: 'none', visibility: 'hidden' }}
            />

            {/* ОТРЕНДЕРЕННЫЙ ШИТ */}
            {sheetData && (
                <Workbook data={sheetData} lang="ru" showToolbar={false} showFormulaBar={true} />
            )}
        </Box>

    );
}
