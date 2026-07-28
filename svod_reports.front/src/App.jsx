import React, { useState } from 'react';
import { Button, Container, Typography, Grid, Paper, Box, Dialog, DialogContent, AppBar, Toolbar, IconButton } from '@mui/material';
import DescriptionIcon from '@mui/icons-material/Description';
import TableChartIcon from '@mui/icons-material/TableChart'; // Иконка таблицы для Excel
import CloseIcon from '@mui/icons-material/Close';
import ReportsView from './ReportsView.jsx';
import ReportsView2 from './ReportsView2.jsx';

export default function App() {
    // Состояния для окон (если понадобятся в будущем) 
    const [openReport1, setOpenReport1] = useState(false);
    const [openReport2, setOpenReport2] = useState(false);
    const [openReport3, setOpenReport3] = useState(false);
    const [openReport4, setOpenReport4] = useState(false);
    const [openReport5, setOpenReport5] = useState(false);

    const [selectedReportId, setSelectedReportId] = useState(null);
    const [selectedReportId2, setSelectedReportId2] = useState(null);

    // Функции для закрытия модальных окон 
    const handleCloseReport = () => {
        setSelectedReportId(null);
    };

    const handleCloseReport2 = () => {
        setSelectedReportId2(null);
    };

    // Стили для кнопок-плиток 
    const buttonStyle = {
        py: 3,
        px: 2,
        fontSize: '1rem',
        fontWeight: 500,
        textTransform: 'none',
        borderRadius: '12px',
        backgroundColor: '#2563eb',
        boxShadow: '0 4px 12px 0 rgba(37, 99, 235, 0.2)',
        transition: 'all 0.2s ease-in-out',
        display: 'flex',
        flexDirection: 'column',
        gap: 1,
        '&:hover': {
            backgroundColor: '#1d4ed8',
            boxShadow: '0 6px 16px 0 rgba(37, 99, 235, 0.3)',
            transform: 'translateY(-2px)',
        }
    };

    // Специфичный стиль для кнопки Excel (зеленый цвет для наглядности, по желанию можно оставить синий)
    const excelButtonStyle = {
        ...buttonStyle,
        backgroundColor: '#16a34a',
        boxShadow: '0 4px 12px 0 rgba(22, 163, 74, 0.2)',
        '&:hover': {
            backgroundColor: '#15803d',
            boxShadow: '0 6px 16px 0 rgba(22, 163, 74, 0.3)',
            transform: 'translateY(-2px)',
        }
    };

    return (
        <Box sx={{ minHeight: '80vh', display: 'flex', alignItems: 'center', py: 4 }}>
            <Container maxWidth="md"> {/* Увеличил до md, чтобы две кнопки красиво вставали в ряд */}
                <Paper elevation={4} sx={{ p: 4, borderRadius: '20px', backgroundColor: '#ffffff' }}>
                    <Typography variant="h5" gutterBottom sx={{ mb: 4, fontWeight: 700, color: '#1e293b' }}>
                        Модуль сводной отчетности
                    </Typography>

                    <Grid container spacing={3} justifyContent="center">
                        {/* КНОПКА №1 - PDF */}
                        <Grid item xs={12} sm={6}>
                            <Button
                                variant="contained"
                                fullWidth
                                sx={buttonStyle}
                                onClick={() => setSelectedReportId(1)}
                            >
                                <DescriptionIcon sx={{ fontSize: '1.8rem' }} />
                                Тестовый PDF
                            </Button>
                        </Grid>

                        {/* КНОПКА №2 - EXCEL (ДОБАВЛЕНАЯ) */}
                        <Grid item xs={12} sm={6}>
                            <Button
                                variant="contained"
                                fullWidth
                                sx={excelButtonStyle}
                                onClick={() => setSelectedReportId2(2)} // Задаем ID для Excel отчета
                            >
                                <TableChartIcon sx={{ fontSize: '1.8rem' }} />
                                Тестовый Excel
                            </Button>
                        </Grid>
                    </Grid>
                </Paper>
            </Container>

            {/* ВСПЛЫВАЮЩЕЕ ОКНО ДЛЯ PDF ОТЧЕТА */}
            <Dialog
                fullScreen
                open={Boolean(selectedReportId)}
                onClose={handleCloseReport}
            >
                <AppBar sx={{ position: 'relative', backgroundColor: '#1e293b' }}>
                    <Toolbar sx={{ display: 'flex', justifyContent: 'space-between' }}>
                        <Typography variant="h6" component="div">
                            Просмотр PDF отчета #{selectedReportId}
                        </Typography>
                        <IconButton edge="end" color="inherit" onClick={handleCloseReport} aria-label="close">
                            <CloseIcon />
                        </IconButton>
                    </Toolbar>
                </AppBar>
                <DialogContent sx={{ p: 0, backgroundColor: '#f8fafc', overflow: 'hidden' }}>
                    <ReportsView reportId={selectedReportId} />
                </DialogContent>
            </Dialog>

            {/* ВСПЛЫВАЮЩЕЕ ОКНО ДЛЯ EXCEL ОТЧЕТА (ДОБАВЛЕННОЕ) */}
            <Dialog
                fullScreen
                open={Boolean(selectedReportId2)}
                onClose={handleCloseReport2}
            >
                <AppBar sx={{ position: 'relative', backgroundColor: '#16a34a' }}> {/* Зеленая шапка для Excel */}
                    <Toolbar sx={{ display: 'flex', justifyContent: 'space-between' }}>
                        <Typography variant="h6" component="div">
                            Просмотр Excel отчета #{selectedReportId2}
                        </Typography>
                        <IconButton edge="end" color="inherit" onClick={handleCloseReport2} aria-label="close">
                            <CloseIcon />
                        </IconButton>
                    </Toolbar>
                </AppBar>
                <DialogContent sx={{ p: 0, backgroundColor: '#f8fafc', overflow: 'hidden' }}>
                    {/* Передаем ID во второй компонент предпросмотра */}
                    <ReportsView2 reportId={selectedReportId2} />
                </DialogContent>
            </Dialog>
        </Box>
    );
}
