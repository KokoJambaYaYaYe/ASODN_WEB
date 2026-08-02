import React, { useState, useEffect } from 'react';
import { Box, Typography, CircularProgress } from '@mui/material';

export default function ReportsView({ reportId }) {
    const VITE_BACKEND_API_URL = import.meta.env.VITE_BACKEND_API_URL;

    const [pdfUrl, setPdfUrl] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    useEffect(() => {
        // Если id отчета не выбран, ничего не делаем
        if (!reportId) {
            setPdfUrl(null);
            return;
        }

        setLoading(true);
        setError(null);

        // Выполняем контролируемый запрос в контексте текущей авторизованной сессии React
        fetch(`${VITE_BACKEND_API_URL}/get-pdf-report?id=${reportId}`, {
            method: 'GET',
            // Если для Windows Auth / Kerberos нужны куки сессии, раскомментируйте строку ниже:
            credentials: 'include' 
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`Ошибка генерации FastReport (Код: ${response.status})`);
                }
                return response.blob(); // Читаем бинарный поток байт от бэкенда
            })
            .then(rawBlob => {
                // Явно зашиваем тип контента, чтобы плагин браузера точно распознал PDF
                const pdfBlob = new Blob([rawBlob], { type: 'application/pdf' });

                // Создаем виртуальный URL формата blob:https://mod.com...
                const localUrl = URL.createObjectURL(pdfBlob);

                setPdfUrl(localUrl);
                setLoading(false);
            })
            .catch(err => {
                console.error("Критическая ошибка загрузки отчета:", err);
                setError(err.message);
                setLoading(false);
            });

        // Важнейший шаг: очищаем память устройства от старого Blob при смене отчета или закрытии вкладки
        return () => {
            if (pdfUrl) {
                URL.revokeObjectURL(pdfUrl);
            }
        };
    }, [reportId, VITE_BACKEND_API_URL]);

    // Сценарий 1: Отчет еще не выбран пользователем
    if (!reportId) {
        return (
            <Box sx={{ p: 4, textAlign: 'center', border: '1px dashed #cbd5e1', borderRadius: '12px', mt: 2 }}>
                <Typography variant="body1" color="text.secondary">
                    Выберите отчет для просмотра.
                </Typography>
            </Box>
        );
    }

    // Сценарий 2: Идет генерация отчета на бэкенде .NET
    if (loading) {
        return (
            <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: 'calc(100vh - 64px)', gap: 2 }}>
                <CircularProgress size={50} />
                <Typography variant="body1" color="text.secondary">
                    Генерация отчета FastReport... Пожалуйста, подождите.
                </Typography>
            </Box>
        );
    }

    // Сценарий 3: Бэкенд выкинул ошибку (например, пустая БД или сбой Kerberos)
    if (error) {
        return (
            <Box sx={{ p: 4, textAlign: 'center', border: '1px solid #fca5a5', bgcolor: '#fef2f2', borderRadius: '12px', mt: 2 }}>
                <Typography variant="body1" color="error.main" fontWeight="bold">
                    {error}
                </Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Попробуйте обновить страницу или обратиться к администратору системы.
                </Typography>
            </Box>
        );
    }

    // Сценарий 4: Успешный рендеринг готового PDF
    return (
        <Box sx={{ width: '100%', height: 'calc(100vh - 64px)' }}>
            {/* Теперь src завязан на blob:// в ОЗУ. Браузер больше не пойдет во внешнюю сеть и отобразит документ */}
            {pdfUrl && (
                <iframe
                    title={`FastReport-PDF-Container-${reportId}`}
                    src={pdfUrl}
                    style={{ width: '100%', height: '100%', border: 'none' }}
                />
            )}
        </Box>
    );
}
